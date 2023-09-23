//#define UCI

using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.API.BitboardHelper;

public class MyBot : IChessBot
{
	private Move bestMoveRoot;

	// put the delta here to make c# shut up about unitialized variables
	private int nodes, phase, evalMG, evalEG, sq, it, psqtIdx, delta;

	// first index 0-1
	//     0 = middlegame
	//     1 = endgame
	// second index 0-5
	//     0 = pawn
	//     1 = knight
	//     2 = bishop
	//     3 = rook
	//     4 = queen
	//     5 = king
	// third index 0-63
	//     square
	// index calculation
	//     firstIndex * 384 + secondIndex * 64 + thirdIndex
	byte[] PSQT = new[] {
		35400291882301049321147417925m, 11544482627846822917076575596m, 11478868718363222700911173644m, 6564600073924705837746954277m,
		21438208676581635460518974976m, 20835117346487653108034127173m, 67715257944533072952437218948m, 58710092906951729419078008829m,
		52506791221248529411991380374m, 45364490959869354340623624626m, 41226986566512201394783833m, 21751405672255603979706438912m,
		30126753567019643176377602621m, 18339741564282984290677381469m, 18350607341241698661890934337m, 16790974603602733649775249983m,
		28873031417138191888318418252m, 31095193406274126396403051085m, 9307576742347736125270468119m, 8727330383296036286916662045m,
		9937464980225787806604528640m, 10612358411998331711783049507m, 13651308881626217338180011797m, 13352746719311385035800401202m,
		6523491524214533359232160272m, 13975353017136610098081569814m, 19957459808119401927087622160m, 25831686672525910814390975338m,
		4388623677487191975153598767m, 1572888122977826521659876615m, 18370126263907955199810623309m, 41941572298415131654182029884m,
		29246959294964915466367094607m, 22061881788600702294443909978m, 6120501858841839629573925m, 8500408109232998698255360m,
		24545195439954221795935851280m, 65509668171677303200239439m, 24858254932962859600886972212m, 18047252480926632911920840513m,
		26705498235936524352047893316m, 16777780774768253065142225494m, 11793199301403634539655994922m, 1889650633751531568946813731m,
		13970536202673604344435317792m, 4990626010881461227408729639m, 8376727654362233395311090709m, 9756761227597027118159136m,
		9329460536006026279589781020m, 1557172366114217332009079840m, 9323378090721472919665646880m, 1547510440892418754090047772m,
		8079350700036775438762513684m, 23584052121840957772975378708m, 32004258010940380173567220028m, 26121824478700588388403142263m,
		25174950943954400190297691718m, 20727577346754962593435992m, 20478169188183424023720702000m, 23634954712641074894345027386m,
		33545695119955859302026138438m, 24561025355096513274842606447m, 24534219890260374401077036337m, 3756397642339670155683711825m
	}.SelectMany(decimal.GetBits).Where((x, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).ToArray();

	// Item1 = zobrist key
	// Item2 = score
	// Item3 = bestMove(ushort, move.RawValue)
	// Item4 = depth
	// Item5 = flag, 0 = exact, 1 = upper bound, 2 = lower bound

	public Move Think(Board board, Timer timer)
	{
		bool shouldStop = false;

		// We recreate the history every time to clear it
		// This saves tokens
		var history = new int[2, 4096];
		var killerMoves = new Move[128];
		var ttEntries = new (ulong, int, ushort, byte, byte)[8388608];

		// this is the most horrid bench implementation I have ever written, I wholeheartedly hope that no living creature
		// will ever be closer than 1000 lightyears to such abominations as I have written here.
#if UCI
		//Console.WriteLine($"{timer.MillisecondsRemaining}, lol {timer.OpponentMillisecondsRemaining}");
		if (timer.MillisecondsRemaining >= 100000000)
		{
			nodes = 0;
			int benchDepth = timer.OpponentMillisecondsRemaining;
			int startTime = timer.MillisecondsElapsedThisTurn;
			for (int depth = 1, alpha = -64000, beta = 64000; depth <= benchDepth; delta *= 2)
			{
				int eval = Search(depth, alpha, beta, false, 0);
				//Console.WriteLine($"Mine Depth: {depth}, Move: {bestMoveRoot} eval: {eval}, nodes: {nodes}, alpha: {alpha}, beta: {beta}");
				//Console.WriteLine($"Timer: {timer.MillisecondsElapsedThisTurn}, t: {timer.MillisecondsRemaining / 15}");
				if (timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 50)
					break;
				if (eval <= alpha)
					alpha -= delta;
				else if (eval >= beta)
					beta += delta;
				else
				{
					delta = ++depth <= 6 ? 64000 : 15;
					alpha = eval - delta;
					beta = eval + delta;
				}

				// if this depth takes up more than 50% of allocated time, there is a good chance that the next search won't finish.
				//if (shouldStop || timer.MillisecondsElapsedThisTurn > millisAlloced / 2)
				//break;
				//Console.WriteLine(nodes);
				//Console.ForegroundColor = ConsoleColor.Green;
			}
			int endTime = timer.MillisecondsElapsedThisTurn;
			int time = endTime - startTime;

			Console.WriteLine($"Bench Depth: {benchDepth}, nodes: {nodes}, time: {time}");
			return default;
		}
#endif

		for (int depth = 1, alpha = -64000, beta = 64000;; delta *= 2)
		{
			int eval = Search(depth, alpha, beta, false, 0);
			//Console.WriteLine($"Mine Depth: {depth}, Move: {bestMoveRoot} eval: {eval}, nodes: {nodes}, alpha: {alpha}, beta: {beta}");
			//Console.WriteLine($"Timer: {timer.MillisecondsElapsedThisTurn}, t: {timer.MillisecondsRemaining / 15}");
			if (timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 50)
				break;
			if (eval <= alpha)
				alpha -= delta;
			else if (eval >= beta)
				beta += delta;
			else
			{
				delta = ++depth <= 6 ? 64000 : 15;
				alpha = eval - delta;
				beta = eval + delta;
			}

			// if this depth takes up more than 50% of allocated time, there is a good chance that the next search won't finish.
			//if (shouldStop || timer.MillisecondsElapsedThisTurn > millisAlloced / 2)
			//break;
			//Console.WriteLine(nodes);
			//Console.ForegroundColor = ConsoleColor.Green;
		}
		// this utilizes partial search results
		return bestMoveRoot;

		// use local function to access board and timer with no token overhead
		// idea from antares
		int Search(int depth, int alpha, int beta, bool doNull, int ply)
		{
			// local search function to save tokens, idea from Tyrant
			int LocalSearch(int localAlpha, int R = 1, bool localDoNull = true) => it = -Search(depth - R, -localAlpha, -alpha, localDoNull, ply + 1);

			// cache in check, used often
			bool inCheck = board.IsInCheck();

			// Certain things should not be done at the root node
			if (ply > 0)
			{
				// check extension
				if (inCheck)
					depth++;

				// check for time up
				// the node check is to avoid checking the timer too often, which can degrade the search speed
				// calling the timer is not a cheap operation
				if ((nodes++ & 2047) == 0 && timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 20 || shouldStop)
				{
					shouldStop = true;
					return alpha;
				}

				// check for drawn position
				// insuf material is not important and doesn't gain much elo
				// TODO: 50 mr probably isn't very important either, try removing it
				if (board.IsRepeatedPosition() || board.FiftyMoveCounter >= 100)
					return 0;
			}

			bool notPV = beta - alpha == 1, isQSearch = depth <= 0, canFPrune = false;

			var (ttKey, ttScore, ttMove, ttDepth, ttType) = ttEntries[board.ZobristKey % 8388608];

			// tt cutoffs
			// ttType stuff is a token optimization from cj
			if (notPV && ttKey == board.ZobristKey && ttDepth >= depth && (ttScore >= beta ? ttType > 1 : ttType < 3))
				return ttScore;


			phase = evalMG = evalEG = it = 0;

			// incremented on line 267
			// evaluation based on material and piece square tables
			// loop through all pieces
			for (; it < 6; it++)
				// loop through side to move(1 = white, 0 = black)
				for (int stm = 2; --stm >= 0; evalMG *= -1, evalEG *= -1)
					// loop through bit indices in pieceBB
					for (ulong pieceBB = board.GetPieceBitboard((PieceType)it + 1, stm == 1); pieceBB != 0;)
					{
						// get the square, and flip it's y value if stm is white(piece square tables are black relative)
						sq = ClearAndGetIndexOfLSB(ref pieceBB) ^ stm * 0b111000;
						// add the middlegame piece square table and material value
						evalMG += PSQT[psqtIdx = it * 64 + sq] + (int)(926193849341991 >> it * 10 & 1023);
						// add the endgame piece square table and material value
						evalEG += PSQT[384 + psqtIdx] + (int)(801985503854668 >> it * 10 & 1023);
						// add the phase
						phase += 17480 >> 3 * it & 7;
						// bitwise operations are used to save tokens

						// bishop pair
						// putting inside the bit loop was Tyrants idea

						// this technically isn't the same as an actual bishop pair evaluation, because if
						// there are more than 2 bishops on the board, it will add the bishop pair bonus twice
						// but in practice, this has no effect on playing strength. The added bonus to a
						// bishop promotion is completely dwarfed by the value of a queen promotion
						if (it == 2 && pieceBB != 0)
						{
							evalMG += 14;
							evalEG += 54;
						}

						if (it == 0 && (pieceBB & (0x0101010101010101u << sq % 8)) != 0)
						{
							evalMG -= 11;
							evalEG -= 26;
						}
					}
			// TODO: check if multiplying endgame eval by (100 - halfMoveClock) / 100 helps with avoiding endgame draws
			int staticEval = 8 + (evalMG * phase + evalEG * (24 - phase)) / (board.IsWhiteToMove ? 24 : -24),
				bestScore = -32000,
				movesPlayed = 0;

			if (isQSearch)
			{
				/* Quiescence search is in the same function as the main search to save tokens
				 * Not sure where this idea originated from
				 */
				bestScore = staticEval;
				if (staticEval >= beta)
					return staticEval;
				if (staticEval > alpha)
					alpha = staticEval;
			}
			// pruning is disabled in pv nodes and when the side to move is in check
			else if (notPV && !inCheck)
			{
				// reverse futility pruning
				/* If, at lower depths, our static eval is above beta by a significant margin
				 * we assume that it will hold above beta and prune the node early
				 * This will inevitably prune some nodes that don't hold above beta
				 * but the increase in search speed is well worth that risk
				 * The depth based margin and depth condition also ensure that rfp won't prune a node if we search far enough
				 */
				if (depth <= 6 && staticEval - depth * 62 >= beta)
					return staticEval;

				// null move pruning
				/*
				 * Make a "null move" and see if we can get a fail high
				 * If we make a null move, we are effectively allowing the opponent to make 2 moves in a row
				 * If the opponent makes 2 moves in a row and still cannot bring the score below beta
				 * Then somewhere up the tree
				 * Disabled if:
				 *     - In Check, position after null move will be illegal
				 *     - Null move was done last move. Double null move just burns 2 plies for nothing
				 *     - Depth is too low for null move to be worth it
				 *     - Only pawns left for side to move, zugzwang becomes extremely common
				 */
				if (doNull && depth >= 3 && phase > 2)
				{
					board.ForceSkipTurn();
					// it isn't used anymore so we can reuse it
					LocalSearch(beta, 2 + depth / 3, false);
					board.UndoSkipTurn();
					if (it >= beta)
						return it;
				}

				// margin for futility pruning
				canFPrune = depth <= 5 && staticEval + depth * 130 + 80 <= alpha;
			}

			// stack allocated moves are quite a bit faster
			Span<Move> moves = stackalloc Move[256];
			board.GetLegalMovesNonAlloc(ref moves, isQSearch);

			if (moves.Length == 0 && !isQSearch)
				return inCheck ? ply - 32000 : 0;

			// move ordering with TT, MVV_LVA, killer moves, and history
			// move scores are negated because sorting defaults to non-decreasing
			Span<int> moveScores = stackalloc int[moves.Length];
			it = 0;
			foreach (Move move in moves)
				moveScores[it++] =
					// tt move ordering, use the move in the transposition table as the first move
					move.RawValue == ttMove ? -1000000 :
					// order noisy moves(moves that directly affect the material balance) by MVP MVV LVA
					// (1) most valuable promotion
					// (2) most valuable victim(captured piece)
					// (3) least valuable attacker(moving piece)
					move.IsCapture || move.IsPromotion ?
						(int)move.MovePieceType - 6 * (int)move.CapturePieceType - 36 * (int)move.PromotionPieceType :
					// Use the killer moves from current ply to order first quiet moves
					move == killerMoves[ply] ? 100 :
					// Order the rest of the quiet moves by their history score
					2000000000 - history[ply & 1, move.RawValue & 4095];

			// sort moves
			MemoryExtensions.Sort(moveScores, moves);

			Move bestMove = default;
			ttType = 1;
			foreach (Move move in moves)
			{
				bool isQuiet = !move.IsCapture && !move.IsPromotion;
				// Late move Pruning
				/* Late Move Pruning
				 * If this node does not seem promising and the depth is low enough
				 * We stop searching if we've played a certain number of depth dependent moves
				 * Most top engines use depth <= 8 and movesPlayed >= 3 + depth * depth / (improving ? 2 : 1) as the canonical formula
				 * But this formula didn't work for my challenge engine
				 */
				// Futility Pruning
				/* If our static evaluation is below alpha by a significant margin, we stop searching after all tactical moves are searched
				 */
				if (notPV && !inCheck && isQuiet && depth <= 3 && movesPlayed >= depth * 10 || canFPrune && isQuiet)
					break;

				board.MakeMove(move);
				// PVS
				/* Disabled in quiescence search
				 * Search the first move with a full [-beta, -alpha] window
				 *
				 * Search the rest of the moves with zero window [-alpha - 1, -alpha]
				 * If a move does not fail low on a [-alpha - 1, -alpha] window, and we aren't already in a zero window
				 * we research with the full window
				 *
				 * Searching with a zero window is faster than a full window
				 * This technique scales with good move ordering. The more researches we do, the worse it performs
				 */

				/* Late move reductions
				 * If a move appears late in the move list and does not seem very good, then we will reduce it's depth.
				 * If the reduced depth search does not fail low, we research with the full depth
				 * This will inevitably reduce late moves that are good, but the increase in search speed is well worth it.
				 */
				int reduction = movesPlayed >= (notPV ? 3 : 5) &&
					depth >= 3 &&
					isQuiet ? 2 + depth / 8 + movesPlayed / 19 : 1;

				if (movesPlayed++ == 0 || isQSearch || LocalSearch(alpha + 1, reduction) > alpha && reduction > 1 | !notPV)
					LocalSearch(beta);

				board.UndoMove(move);

				// return early if time is up
				if (shouldStop)
					return alpha;

				// update the best score
				if (it > bestScore)
				{
					bestScore = it;

					if (it > alpha)
					{
						if (ply == 0)
							bestMoveRoot = move;
						alpha = it;
						bestMove = move;
						ttType = 2;
					}
					if (alpha >= beta)
					{
						// on a fail high, we update the killer moves and history
						if (!isQSearch && isQuiet)
						{
							killerMoves[ply] = bestMove;
							history[ply & 1, bestMove.RawValue & 4095] += depth * depth;
						}
						ttType++;
						break;
					}
				}
			}

			// prevent negative depth from overflowing in transposition table
			ttEntries[board.ZobristKey % 8388608] = (board.ZobristKey, bestScore, bestMove.RawValue, (byte)Math.Max(depth, 0), ttType);

			return bestScore;
		}
	}
}
