using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.API.BitboardHelper;

public class MyBotOld : IChessBot
{
	private Move bestMoveRoot;

	private int nodes, phase, evalMG, evalEG, sq, it, psqtIdx;
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
	int[] history;
	byte[] PSQT;
	Move[] killerMoves = new Move[128];

	// Item1 = zobrist key
	// Item2 = score
	// Item3 = bestMove(ushort, move.RawValue)
	// Item4 = depth
	// Item5 = flag, 0 = exact, 1 = upper bound, 2 = lower bound
	(ulong, int, ushort, byte, byte)[] ttEntries = new (ulong, int, ushort, byte, byte)[8388608];

	public MyBotOld()
	{
		// using a constructor is probably a hacky way to initialize the tables, but I can't think of any other way
		PSQT = new[] {
			21094807297396170059121950720m, 4063573671909118900332283728m, 13033580525369532380312773654m, 7185973889366643949998973738m,
			1748579867707254027m, 27016260853506898115996680192m, 65527050283015909919749346162m, 58388442896095014640267493357m,
			54684114159199641910293669278m, 50643959924137751381160603318m, 11835606766309804905693615997m, 31049414878835688434623459328m,
			30131617623882172976243756611m, 18362768100751210731109765984m, 23931136485047039527984126555m, 21756127707636350335108735568m,
			27320732735359262570281455691m, 23955310591146359170559469381m, 11791962098175803135329117976m, 11828315187793339619361105451m,
			15531240618538919563295072270m, 8092994930502147825766050613m, 17691543696897469162351235346m, 14890533585169542596569489729m,
			11799314820692613957051817497m, 13066321358369413249314466850m, 40394394047467713944602286631m, 11834823737971881719628465807m,
			17127160077172742715505484641m, 1292379961320313844599303467m, 1599646640417935696172305729m, 22995139756968247863879739408m,
			32657400638508116903907557376m, 23606893782654837637738557027m, 621454355010524025132882723m, 2174881309614519538179244544m,
			506948592361148182m, 58265318176446749004005376m, 23927348847501840024624708146m, 17116355858717656737429798201m,
			25774630077557770114204846399m, 13996028315468822827633624915m, 9613444621270906567248261149m, 38766461525716347799739681m,
			13043304432078124649797198624m, 6528431525675036365660823595m, 12408523670691398397001670676m, 4963963288967702994256206119m,
			5599895937541491329682773272m, 1862983170885230679925590800m, 6836579716156728386784531223m, 928493104557129798850645778m,
			7143637338092731817826389518m, 26380259504769150679046686988m, 22385982481834929839347876417m, 23948109389823398466680471912m,
			19908770626736767907702524731m, 10863573772565898982364300880m, 11489796273973078630559919150m, 20208712009869462661474758438m,
			22051076867450946574639707434m, 18341007028651184920105406026m, 23907967870025172194528080930m, 4995513476589039188639958603m
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
		history = new int[8192];
		for (int i = 1; i < 128;)
		{
			Search(i++, -200000, 200000, false, 0);
			// if this depth takes up more than 50% of allocated time, there is a good chance that the next search won't finish.
			//if (shouldStop || timer.MillisecondsElapsedThisTurn > millisAlloced / 2)
			//break;
			//Console.WriteLine(nodes);
			//Console.ForegroundColor = ConsoleColor.Green;
			//Console.WriteLine($"Mine Depth: {i - 1}, Move: {bestMoveRoot} eval: {eval}, nodes: {nodes}");
		}
		// this utilizes partial search results
		return bestMoveRoot;




		// use local function to access board and timer with no token overhead
		// idea from antares
		int Search(int depth, int alpha, int beta, bool doNull, int ply)
		{
			// local search function to save tokens, idea from Tyrant
			int LocalSearch(int localAlpha, int R = 1, bool localDoNull = true) => -Search(depth - R, -localAlpha, -alpha, localDoNull, ply + 1);

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
				if ((nodes++ & 2047) == 0 && timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 40 || shouldStop)
				{
					shouldStop = true;
					return alpha;
				}

				// check for drawn position
				// insuf material is not important and doesn't gain much elo
				if (board.IsRepeatedPosition() || board.FiftyMoveCounter >= 100)
					return 0;
			}

			bool notPV = beta - alpha == 1, isQSearch = depth <= 0;

			ref var ttEntry = ref ttEntries[board.ZobristKey % 8388608];

			// tt cutoffs
			if (notPV && ttEntry.Item1 == board.ZobristKey && ttEntry.Item4 >= depth &&
				(ttEntry.Item5 == 1 ||
				ttEntry.Item5 == 2 && ttEntry.Item2 <= alpha ||
				ttEntry.Item5 == 3 && ttEntry.Item2 >= beta))
				return ttEntry.Item2;


			phase = evalMG = evalEG = it = 0;

			// incremented on line 267
			// evaluation based on material and piece square tables
			// loop through all pieces
			for (; it < 6; it++)
			{
				// loop through side to move(1 = white, 0 = black)
				for (int stm = 2; --stm >= 0;)
				{
					ulong pieceBB = board.GetPieceBitboard((PieceType)(it + 1), stm == 1);

					// loop through bit indices in pieceBB
					while (pieceBB != 0)
					{
						// get the square, and flip it's y value if stm is white(piece square tables are black relative)
						sq = ClearAndGetIndexOfLSB(ref pieceBB) ^ stm * 0b111000;
						// add the middlegame piece square table and material value
						evalMG += PSQT[psqtIdx = it * 64 + sq] + (int)(799681242290199 >> it * 10 & 1023);
						// add the endgame piece square table and material value
						evalEG += PSQT[384 + psqtIdx] + (int)(700778869297214 >> it * 10 & 1023);
						// add the phase
						phase += 17480 >> 3 * it & 7;
						// bitwise operations are used to save tokens
					}
					evalMG *= -1;
					evalEG *= -1;
				}
			}
			// TODO: check if multiplying endgame eval by (100 - halfMoveClock) / 100 helps with avoiding endgame draws
			int staticEval = (evalMG * phase + evalEG * (24 - phase)) / (board.IsWhiteToMove ? 24 : -24), bestScore = -32000, movesPlayed = 0;

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
				if (depth <= 6 && staticEval - depth * 55 >= beta)
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
					it = LocalSearch(beta, 2 + depth / 3, false);
					board.UndoSkipTurn();
					if (it >= beta)
						return it;
				}
			}

			// stack allocated moves are quite a bit faster
			Span<Move> moves = stackalloc Move[256];
			board.GetLegalMovesNonAlloc(ref moves, isQSearch);

			if (moves.Length == 0 && !isQSearch)
				return inCheck ? ply - 32000 : 0;

			// move ordering with TT, MVV_LVA, killer moves, and history
			// move scores are negated because sorting defaults to non-decreasing
			Span<int> moveScores = stackalloc int[moves.Length];
			for (int i = 0; i < moves.Length; i++)
				moveScores[i] =
					// tt move ordering, use the move in the transposition table as the first move
					moves[i].RawValue == ttEntry.Item3 ? -1000000 :
					// order noisy moves(moves that directly affect the material balance) by MVP MVV LVA
					// (1) most valuable promotion
					// (2) most valuable victim(captured piece)
					// (3) least valuable attacker(moving piece)
					moves[i].IsCapture || moves[i].IsPromotion ?
						(int)moves[i].MovePieceType - 6 * (int)moves[i].CapturePieceType - 36 * (int)moves[i].PromotionPieceType :
					// Use the killer moves from current ply to order first quiet moves
					moves[i] == killerMoves[ply] ? 100 :
					// Order the rest of the quiet moves by their history score
					2000000000 - history[moves[i].RawValue & 4095 + (board.IsWhiteToMove ? 0 : 4096)];

			// sort moves
			MemoryExtensions.Sort(moveScores, moves);

			Move bestMove = default;
			byte ttType = 2;
			foreach (Move move in moves)
			{
				bool isQuiet = !move.IsCapture && !move.IsPromotion;
				if (notPV && !inCheck && isQuiet && depth <= 3 && movesPlayed >= depth * 10)
					break;
				board.MakeMove(move);

				int score;
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

				if (movesPlayed++ == 0 || isQSearch || (score = LocalSearch(alpha + 1, reduction)) > alpha && (reduction > 1 || !notPV))
					score = LocalSearch(beta);

				board.UndoMove(move);

				// return early if time is up
				if (shouldStop)
					return alpha;

				// update the best score
				if (score > bestScore)
				{
					bestScore = score;
					if (ply == 0)
						bestMoveRoot = move;

					if (score > alpha)
					{
						alpha = score;
						bestMove = move;
						ttType = 1;
					}
					if (alpha >= beta)
					{
						// on a fail high, we update the killer moves and history
						if (!isQSearch && isQuiet)
						{
							killerMoves[ply] = bestMove;
							history[bestMove.RawValue & 4095 + (board.IsWhiteToMove ? 0 : 4096)] += depth * depth;
						}
						ttType = 3;
						break;
					}
				}
			}

			// prevent negative depth from overflowing in transposition table
			ttEntry = (board.ZobristKey, bestScore, bestMove.RawValue, (byte)Math.Max(depth, 0), ttType);

			return bestScore;
		}
	}
}
