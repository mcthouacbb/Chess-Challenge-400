#define UCI

using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.API.BitboardHelper;

public class MyBot : IChessBot
{
	private Move bestMoveRoot;

	// put the delta here to make c# shut up about unitialized variables
	private int nodes, phase, evalMG, evalEG, sq, it, psqtIdx, delta;
	private bool shouldStop;

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
	byte[] PSQT;

	// Item1 = zobrist key
	// Item2 = score
	// Item3 = bestMove(ushort, move.RawValue)
	// Item4 = depth
	// Item5 = flag, 0 = exact, 1 = upper bound, 2 = lower bound

	public MyBot()
	{
		// using a constructor is probably a hacky way to initialize the tables, but I can't think of any other way
		PSQT = new[] {
			36014411994817024531219564359m, 11539627997992206830351311462m, 11787149524659654389158387725m, 6875279824090968596597448485m,
			22059606029562630386530326272m, 21446838662922509742590347079m, 68334227945656684353816206466m, 58708888703642431372614172670m,
			52196097285463173046727516308m, 44744311995815625154613717169m, 1279162303268390191957902420m, 21445547458123954461288109824m,
			30436229095286312676494038331m, 18029052332633538399639656542m, 18039913387009878602677569089m, 16171995139082960385936083519m,
			29805108520357794161329066062m, 31408319397691931342320136529m, 9926556188276928308111419161m, 9038024355902543929591403294m,
			10248158934457888245594721536m, 10609964209373794976648993316m, 13959580188282084706610906390m, 13663454822123007611288639538m,
			6832971830044082042278122256m, 13667091100305822902961773079m, 22165001317274436389701551889m, 33571225140001447208779740549m,
			3467474188099714437775392565m, 333814660396047155746645006m, 18679625459275489648333969485m, 41940358631566963923475318842m,
			30488540334293500804530786641m, 22679628733914908093055722334m, 3697964817827716725087525m, 626237871652021990419005440m,
			25166592792934647179151872531m, 70354853293613515050930513m, 26411748278629947916989580344m, 19604363233106857158973998150m,
			28568471832236762856069157193m, 18020584944198184223109369947m, 11794413005076053092628112686m, 2200354069610088093653080869m,
			14901423306217113697112104227m, 5613227567387364044929574954m, 8998115544163239236647460628m, 12184057743908308084201503m,
			9640159175566722565794766108m, 1246468985668233205530956319m, 9013883636166599165991918624m, 1238015967891643148301900315m,
			8078137051778337775728006163m, 22648310406745462318803522068m, 31074584628720298169310665013m, 24258841419222049658240785270m,
			23620272192283973572196585025m, 15858761801828818975937365m, 22333884487669623263972830248m, 24261216177319878837210793787m,
			36339551134038947213601168204m, 26735897238353871726596486006m, 26399616171392032770979810360m, 5623016979495495128997778264m
		}.SelectMany(decimal.GetBits).Where((x, i) => i % 4 != 3).SelectMany(BitConverter.GetBytes).ToArray();

		// uncomment to print out eval parameters
		/*for (int i = 0; i < 3; i++)
		{
			Console.Write("{");
			for (int j = 0; j < 6; j++)
			{
				Console.Write($"{MAT_PHASE[6 * i + j]}, ");
			}
			Console.WriteLine("}");
		}

		for (int i = 0; i < 2; i++)
        {
            Console.WriteLine("{");
            for (int j = 0; j < 6; j++)
            {
                Console.WriteLine($"\t{((PieceType)(j + 1)).ToString()}");
                Console.WriteLine("\t{");
                for (int y = 0; y < 8; y++)
                {
                    Console.Write("\t\t");
                    for (int x = 0; x < 8; x++)
                    {
                        Console.Write($"{PSQT[384 * i + 64 * j + 8 * y + x],4}, ");
                    }
                    Console.WriteLine();
                }
                Console.WriteLine("\t}");
            }
            Console.WriteLine("}");
        }*/
	}

	public Move Think(Board board, Timer timer)
	{
		shouldStop = false;

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
					delta = ++depth <= 6 ? 64000 : 25;
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
				delta = ++depth <= 6 ? 64000 : 25;
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
			if (notPV && ttKey == board.ZobristKey && ttDepth >= depth && (ttScore >= beta ? ttType > 1 : ttType < 3))
				return ttScore;


			phase = evalMG = evalEG = it = 0;

			// incremented on line 267
			// evaluation based on material and piece square tables
			// loop through all pieces
			for (; it < 6; it++)
				// loop through side to move(1 = white, 0 = black)
				for (int stm = 2; --stm >= 0;)
				{
					ulong pieceBB = board.GetPieceBitboard((PieceType)it + 1, stm == 1);


					// loop through bit indices in pieceBB
					while (pieceBB != 0)
					{
						// get the square, and flip it's y value if stm is white(piece square tables are black relative)
						sq = ClearAndGetIndexOfLSB(ref pieceBB) ^ stm * 0b111000;
						// add the middlegame piece square table and material value
						evalMG += PSQT[psqtIdx = it * 64 + sq] + (int)(951493364694056 >> it * 10 & 1023);
						// add the endgame piece square table and material value
						evalEG += PSQT[384 + psqtIdx] + (int)(829485109904462 >> it * 10 & 1023);
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
							evalEG += 51;
						}
					}
					evalMG *= -1;
					evalEG *= -1;
				}
			// TODO: check if multiplying endgame eval by (100 - halfMoveClock) / 100 helps with avoiding endgame draws
			int staticEval = (evalMG * phase + evalEG * (24 - phase)) / (board.IsWhiteToMove ? 24 : -24),
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
				if (notPV && !inCheck && isQuiet && depth <= 3 && movesPlayed >= depth * 10)
					break;
				if (canFPrune && isQuiet)
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
