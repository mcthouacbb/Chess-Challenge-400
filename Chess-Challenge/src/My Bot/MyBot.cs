#define UCI

using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.API.BitboardHelper;

public class MyBot : IChessBot
{
	private Move bestMoveRoot;

	// put the delta here to make c# shut up about unitialized variables
	private int nodes, phase, eval, sq, it, psqtIdx, delta;

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
	int[] PSQT = new[] {
		155115573901849424484650821m, 11581929810729250749183184709m, 13458178680261725682006511429m, 20503775150684365782873100101m,
		18078632324679887908666494789m, 19646580925451093867347005253m, 18432738901106473632080744261m, 2680548070311818955896082245m,
		18331225934292947613224568932m, 24573046631984553647849505903m, 26697252189190330602207019106m, 23974821930842142811311267442m,
		26746931484230388816334379628m, 29856114812413046843702797157m, 29235907108185974045736863534m, 23621458184198210323206999315m,
		21721068173446087279921617689m, 25843571331540134251582152745m, 30106376053174570572835473730m, 31334635536103481677071398724m,
		32268001936675502963661025353m, 31104897884464874476888074591m, 30489413118293596367389870413m, 26079180781939635639474013989m,
		19872648708232614394437051660m, 27601401290013895093409029920m, 31004570084713129760438292771m, 33441854224838814010535250213m,
		34361894070298176624047227702m, 33160188896394478252558190642m, 31613897270298735218047259444m, 26325966120731990527844356635m,
		17086079471363656586494349568m, 24811191053675101151985471513m, 30056757945614189255759693079m, 33424915096116416640405536805m,
		33429736632151033179373305893m, 31896757375683390380826756128m, 28498415060680867587296528679m, 24455701062023285281952433419m,
		15258122240979798426726632448m, 21459921343324595230168649493m, 26357416657942117753098339607m, 29441353385948914822909856789m,
		29750871470495279585206339619m, 28204622401105951227560658972m, 23296303476419463469157058358m, 20799824324923766351322021909m,
		11620445541701742975126343680m, 18386808205653945859862500118m, 21774190184111090101852114193m, 24520907425278896114478485769m,
		25141105222881867112728367386m, 23301011639911445093278550825m, 18706999151332709561695208767m, 14075524149031506218985849105m,
		4799662473694536146668310341m, 7923526644438270084112600901m, 13156990240497457188876799813m, 18002284885019445357114183493m,
		11265002516619193179236224837m, 17409981662426657415234735941m, 10992923353045448230351294277m, 3877167070226908666575671109m
	}.SelectMany(decimal.GetBits).SelectMany(BitConverter.GetBytes).Chunk(2)/*.Select(a => {
		Console.WriteLine($"{a[0]}, {a[1]}");
		return a[0] + a[1] * 65536;
	}).*/.Select(a => a[0] + a[1] * 65536).ToArray();

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


			phase = eval = it = 0;

			// incremented on line 267
			// evaluation based on material and piece square tables
			// loop through all pieces
			// uses packed evaluation trick to save tokens(thanks to
			for (; it < 6; it++)
				// loop through side to move(1 = white, 0 = black)
				for (int stm = 2; --stm >= 0; eval = -eval)
					// loop through bit indices in pieceBB
					for (ulong pieceBB = board.GetPieceBitboard((PieceType)it + 1, stm == 1); pieceBB != 0;)
					{
						// get the square, and flip it's y value if stm is white(piece square tables are black relative)
						sq = ClearAndGetIndexOfLSB(ref pieceBB) ^ stm * 0b111000;
						// add the packed piece square table and material values
						eval += PSQT[sq * 8 + it] + new[] { 4980775, 10485894, 13697270, 26935673, 47776586, 0 }[it];
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
							eval += 3538958;

						// doubled pawn
						if (it == 0 && (pieceBB & 0x0101010101010101u << sq % 8) != 0)
							eval -= 1703947;
					}
			// TODO: check if multiplying endgame eval by (100 - halfMoveClock) / 100 helps with avoiding endgame draws
			// it hurts me not to use bitwise operations for this stuff, but it's done in the name of tokens
			int staticEval = 8 + ((short)eval * phase + (eval + 0x8000 >> 16) * (24 - phase)) / (board.IsWhiteToMove ? 24 : -24),
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
			moveScores.Sort(moves);

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
