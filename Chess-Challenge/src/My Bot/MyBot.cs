//#define UCI

using ChessChallenge.API;
using System;
using System.Linq;

public class MyBot : IChessBot
{
	private Move bestMoveRoot;

	// put the delta here to make c# shut up about unitialized variables
	private int nodes, phase, eval, sq, it, delta;

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
		162378573907163608468443463m, 12509171266076764537038065991m, 14089261551695647595105571143m, 22351037433687643931806486855m,
		19003484170494650816742838599m, 21187980179940948218151653703m, 19975300079112888281614799175m, 2687811070247606617347215687m,
		19870179131999826413948997223m, 25809768411681260029584968043m, 28245881552641757293457278305m, 24915375737088974826198425980m,
		28903659924196595598264655214m, 31094078408545548091836557405m, 31080713816033830771089640730m, 24864238667859563594204156690m,
		23260016648785637699477266200m, 27081506703938028863917941793m, 31034817044805132858166233405m, 32882065381430657238225992768m,
		33820243947236544512568209989m, 32959385480350578468483704410m, 32646113279690211858442504513m, 27013689959377190020747708962m,
		20802303329675993978549577738m, 28839341440119632895448981530m, 32248550104656124158272475931m, 35603413664164583855827061280m,
		35607092460794033969129849393m, 35017075472828730553059181614m, 32857867882404223461226910256m, 27562687882055139606891926554m,
		18331259009145206108329743360m, 26053981037194044763149767444m, 31294688650843406872773592593m, 35281829969640750762259972641m,
		35285428449506518144594411554m, 33140737414290710117469454369m, 29742395154414218187319347749m, 25997100445211660676862314251m,
		16810359492284245558761819904m, 22707542307684866459682345487m, 28216739956963853927472301070m, 30988773823366239658592439568m,
		30996051026723574284591367966m, 29140326373800249518908507161m, 24852181690656888486452266547m, 22046203362097688778252813078m,
		12256354782080149317318808576m, 18712018788167055605867549713m, 22713516193449595772052442122m, 25459048120630765548933812484m,
		26386289483102799332794043926m, 24240356575610778868202604331m, 19647543568190187961459541564m, 15012441751322451226075137041m,
		5429531826221284093706719559m, 8247537745937350384847180103m, 12552508570564778792259965255m, 18938017191840019227158860103m,
		11892444590785613558071447879m, 18349303023333149394129736007m, 11313293546591776380314865991m, 4196304726628143172615622983m
	}.SelectMany(decimal.GetBits).SelectMany(BitConverter.GetBytes).Chunk(2)/*.Select(a => {
		Console.WriteLine($"{a[0]}, {a[1]}");
		return a[0] + a[1] * 65536;
	}).*/.Select(a => a[0] + a[1] * 65536).ToArray(), staticEvals = new int[256];

	// Item1 = zobrist key
	// Item2 = score
	// Item3 = bestMove(ushort, move.RawValue)
	// Item4 = depth
	// Item5 = flag, 0 = exact, 1 = upper bound, 2 = lower bound

	public Move Think(Board board, Timer timer)
	{
		bool shouldStop = false;
		nodes = 0;
		// We recreate the history every time to clear it
		// This saves tokens
		var history = new int[2, 4096];
		var killerMoves = new Move[128];
		var ttEntries = new (ulong, Move, short, byte, byte)[8388608];

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

				// check for drawn position
				// insuf material is not important and doesn't gain much elo
				// TODO: 50 mr probably isn't very important either, try removing it
				if (board.IsRepeatedPosition() || board.FiftyMoveCounter >= 100)
					return 0;
			}

			bool notPV = beta - alpha == 1, isQSearch = depth <= 0;
			ulong zkey = board.ZobristKey;
			var (ttKey, ttMove, ttScore, ttDepth, ttType) = ttEntries[zkey % 8388608];

			// tt cutoffs
			// ttType stuff is a token optimization from cj
			if (ttKey == zkey && notPV && ttDepth >= depth && (ttScore >= beta ? ttType > 1 : ttType < 3))
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
						sq = BitboardHelper.ClearAndGetIndexOfLSB(ref pieceBB) ^ stm * 0b111000;
						// add the packed piece square table and material values
						eval += PSQT[sq * 8 + it] + new[] { 5111853, 11075723, 14352633, 27459969, 49611616, 0 }[it];
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
							eval += 3407886;

						// rook on semi open and open file
						if (it == 3 && (board.GetPieceBitboard(PieceType.Pawn, stm == 1) & 0x0101010101010101u << sq % 8) == 0)
							eval += 655380;
					}
			// TODO: check if multiplying endgame eval by (100 - halfMoveClock) / 100 helps with avoiding endgame draws
			// it hurts me not to use bitwise operations for this stuff, but it's done in the name of tokens
			int staticEval = staticEvals[ply] = 8 + ((short)eval * phase + (eval + 0x8000 >> 16) * (24 - phase)) / (board.IsWhiteToMove ? 24 : -24),
				bestScore = -32000,
				movesPlayed = 0;

			int improving = Convert.ToInt32(!inCheck && ply > 1 && staticEval > staticEvals[ply - 2]);

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
				if (depth <= 6 && staticEval - (depth - improving) * 80 >= beta)
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
			}

			// stack allocated moves are quite a bit faster
			Span<Move> moves = stackalloc Move[256];
			board.GetLegalMovesNonAlloc(ref moves, isQSearch);

			// move ordering with TT, MVV_LVA, killer moves, and history
			// move scores are negated because sorting defaults to non-decreasing
			Span<int> moveScores = stackalloc int[moves.Length];
			it = 0;
			foreach (Move move in moves)
				moveScores[it++] =
					// tt move ordering, use the move in the transposition table as the first move
					move == ttMove ? -1000000 :
					// order noisy moves(moves that directly affect the material balance) by MVP MVV LVA
					// (1) most valuable promotion
					// (2) most valuable victim(captured piece)
					// (3) least valuable attacker(moving piece)
					move.IsCapture || move.IsPromotion ?
						(int)move.MovePieceType - 6 * (int)move.CapturePieceType :
					// Use the killer moves from current ply to order first quiet moves
					move == killerMoves[ply] ? 100 :
					// Order the rest of the quiet moves by their history score
					2000000000 - history[ply & 1, move.RawValue & 4095];

			if (it == 0 && !isQSearch)
				return inCheck ? ply - 32000 : 0;

			// sort moves
			moveScores.Sort(moves);

			ttType = 1;
			foreach (Move move in moves)
			{
				// check for time up
				// the node check is to avoid checking the timer too often, which can degrade the search speed
				// calling the timer is not a cheap operation

				// we return alpha to prevent the pv from being changed after time is up
				// this allows for partial search results to be used(which saves tokens)(it might also gain elo but I'm not sure)
				if ((++nodes & 2047) == 0 && timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 20 || shouldStop)
				{
					shouldStop = true;
					return alpha;
				}

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
				if (notPV && !inCheck && isQuiet && depth <= 5 && (movesPlayed >= depth * 10 || staticEval + (depth + improving) * 130 - 50 <= alpha))
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
				int reduction = movesPlayed >= 4 &&
					depth >= 3 &&
					isQuiet ? 2 + depth / 8 + movesPlayed / 19 : 1;

				if (movesPlayed++ == 0 || isQSearch || LocalSearch(alpha + 1, reduction) > alpha && reduction > 1 | !notPV)
					LocalSearch(beta);


				board.UndoMove(move);

				// update the best score
				if (it > bestScore)
				{
					bestScore = it;

					if (it > alpha)
					{
						if (ply == 0)
							bestMoveRoot = move;
						alpha = it;
						ttMove = move;
						ttType = 2;
					}
					if (alpha >= beta)
					{
						// on a fail high, we update the killer moves and history
						if (!isQSearch && isQuiet)
						{
							killerMoves[ply] = ttMove;
							history[ply & 1, ttMove.RawValue & 4095] += depth * depth;
						}
						// increment ttType to set to lowerBound
						// I got the idea from jw, but Tyrant said that it originated from Toanth
						ttType++;
						break;
					}
				}
			}

			// prevent negative depth from overflowing in transposition table for qsearch
			ttEntries[zkey % 8388608] = (zkey, ttMove, (short)bestScore, (byte)Math.Max(depth, 0), ttType);

			return bestScore;
		}
	}
}
