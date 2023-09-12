/*using ChessChallenge.API;
using System.Linq;
using System;
using static ChessChallenge.API.BitboardHelper;

public class MyBot : IChessBot
{
    private Move bestRootMove;

    private int millisAlloced, nodes, phase, evalMG, evalEG, sq, it, psqtIdx;
    private bool shouldStop;
    //int nodes = 0;

    // TODO: Remove constants and replace them inline

    // 0-5 = middlegame material, 6-11 = endgame material, 12-17 = phase weights
    short[] MAT_PHASE = new short[18];


	// pack static arrays into 64 bit unsigned integers, and then use Buffer.BlockCopy to extract them

	// these evaluation parameters were obtained from my engine, you can find them at https://github.com/mcthouacbb/Sirius
	// The values are in the main branch under the Sirius/src/eval in material.h, phase.h, and psqt.cpp
	// Additionally, the code for tuning the parameters is in the tune branch under Sirius/src/tune



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

    Move[] killerMoves = new Move[256];
	int[] history;
	byte[] PSQT;

	// Item1 = zobrist key
	// Item2 = score
	// Item3 = bestMove(ushort, move.RawValue)
	// Item4 = depth
	// Item5 = flag, 0 = exact, 1 = upper bound, 2 = lower bound
	ushort[] ttMoves = new ushort[67108864];

	/*static unsafe void DisplaySizeOf<T>() where T : unmanaged
	{
		Console.WriteLine($"Size of {typeof(T)} is {sizeof(T)}");
	}

	public MyBot()
    {
		//DisplaySizeOf<(ulong, int, ushort, byte, byte)>();
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
		// using a constructor is probably a hacky way to initialize the tables, but I can't think of any other way
		//Buffer.BlockCopy(ARRAYS_INIT, 0, MAT_PHASE, 0, 36);
		//Buffer.BlockCopy(ARRAYS_INIT, 40, HALF_PSQT, 0, 384);
		/*for (int ph = 0; ph < 2; ph++)
		{
			Console.WriteLine("{");
			for (int pc = 0; pc < 6; pc++)
			{
				Console.WriteLine("    {");
				for (int y = 0; y < 8; y++)
				{
					Console.Write("        ");
					for (int x = 0; x < 8; x++)
					{
						Console.Write(PSQT[ph * 384 + pc * 64 + y * 8 + x]);
						Console.Write(", ");
					}
					Console.WriteLine();
				}
				Console.WriteLine("    }");
			}
			Console.WriteLine("}");
		}
	}

    public Move Think(Board board, Timer timer)
    {
		history = new int[8192];
        // use 2.5% of remaining time(decent strategy that doesn't take up many tokens)
        millisAlloced = timer.MillisecondsRemaining / 40;
        shouldStop = false;

        for (int i = 1; i < 128;)
        {
            Search(i++, -200000, 200000, true, 0);
            // if this depth takes up more than 50% of allocated time, there is a good chance that the next search won't finish.
            if (shouldStop)
				break;
            //Console.WriteLine(nodes);
            //Console.ForegroundColor = ConsoleColor.Green;
            //Console.WriteLine($"Mine Depth: {i - 1}, Move: {bestRootMove} eval: {eval}");
        }
        return bestRootMove;


		// local search function avoids passing board and timer or setting variables
		// thanks to antares for this idea
		int Search(int depth, int alpha, int beta, bool doNull, int ply)
		{
			int LocalSearch(int localAlpha, int R = 1, bool localDoNull = true) => -Search(depth - R, localAlpha, -alpha, localDoNull, ply + 1);
			bool notPV = alpha - beta == 1;
			// check extension, should not do at root but save tokens for this challenge
			if (board.IsInCheck())
				depth++;

			ulong ttIndex = board.ZobristKey % 67108864;

			bool isQSearch = depth <= 0;
			if ((nodes++ & 2047) == 0 && timer.MillisecondsElapsedThisTurn > millisAlloced || shouldStop)
			{
				shouldStop = true;
				return 0;
			}


			// evaluation
			phase = evalMG = evalEG = it = 0;

			for (; it < 6; it++)
			{
				for (int stm = 2; --stm >= 0;)
				{
					ulong pieceBB = board.GetPieceBitboard((PieceType)(it + 1), stm == 1);

					while (pieceBB != 0)
					{
						sq = ClearAndGetIndexOfLSB(ref pieceBB) ^ (stm * 0b111000);
						evalMG += PSQT[psqtIdx = it * 64 + sq] + (int)(799681242290199 >> it * 10 & 1023);
						evalEG += PSQT[384 + psqtIdx] + (int)(700778869297214 >> it * 10 & 1023);
						phase += 17480 >> 3 * it & 7;
					}

					evalMG = -evalMG;
					evalEG = -evalEG;
				}
			}
			// linearly interpolate between evalMG and evalEG using phase(0-242)
			// TODO: check if multiplying endgame eval by (100 - halfMoveClock) / 100 helps with avoiding endgame draws
			int staticEval = (evalMG * phase + evalEG * (24 - phase)) / (board.IsWhiteToMove ? 24 : -24);

			int bestScore = -32000;

			if (isQSearch)
			{
				bestScore = staticEval;
				if (staticEval >= beta)
					return staticEval;
				if (staticEval > alpha)
					alpha = staticEval;
			}
			else if (notPV && !board.IsInCheck())
			{
				if (depth <= 6 && staticEval - depth * 70 >= beta)
					return staticEval;

				// null move pruning
				/*
				 * Make a "null move" and see if we can get a fail high
				 * Disabled if:
				 *     - In Check, position after null move will be illegal
				 *     - Null move was done last move. Double null move just burns 2 plies for nothing
				 *     - Depth is less than reduction factor
				 *     - Only pawns left for side to move, zugzwang becomes extremely common
				 */
/*GetNumberOfSetBits((board.IsWhiteToMove ? board.WhitePiecesBitboard : board.BlackPiecesBitboard) ^
	board.GetPieceBitboard(PieceType.Pawn, board.IsWhiteToMove))*
if (notPV && doNull && depth >= 3 && GetNumberOfSetBits((board.IsWhiteToMove ? board.WhitePiecesBitboard : board.BlackPiecesBitboard) ^
	board.GetPieceBitboard(PieceType.Pawn, board.IsWhiteToMove)) >= 2)
{
	// in check detection is already done above
	board.ForceSkipTurn();
	int nullScore = LocalSearch(-beta, 2 + depth / 3, false);
	board.UndoSkipTurn();
	if (nullScore >= beta)
		return nullScore;
}
}

Span<Move> moves = stackalloc Move[256];
board.GetLegalMovesNonAlloc(ref moves, isQSearch);

if (moves.Length == 0 && !isQSearch)
return board.IsInCheck() ? ply - 32000 : 0;

// move ordering with TT, MVV_LVA, and killer moves
Span<int> moveScores = stackalloc int[moves.Length];
for (int i = 0; i < moves.Length; i++)
moveScores[i] =
	moves[i].RawValue == ttMoves[ttIndex] ? -1000000 :
	// order by MVP MVV LVA
	// (1) most valuable promotion
	// (2) most valuable victim(captured piece)
	// (3) least valuable attacker(moving piece)
	moves[i].IsCapture || moves[i].IsPromotion ?
		(int)moves[i].MovePieceType - 6 * (int)moves[i].CapturePieceType - 36 * (int)moves[i].PromotionPieceType :
	moves[i] == killerMoves[ply] ? 100 :
	2000000000 - history[moves[i].RawValue & 4095 + (board.IsWhiteToMove ? 0 : 4096)];

MemoryExtensions.Sort(moveScores, moves);

Move bestMove = Move.NullMove;
for (int i = 0; i < moves.Length; i++)
{
Move move = moves[i];
board.MakeMove(move);
int score;
if (board.IsRepeatedPosition())
	score = 0;
else if (i == 0 || isQSearch)
	score = LocalSearch(-beta);
else
{
	score = LocalSearch(-alpha - 1);
	if (score > alpha && !notPV)
		score = LocalSearch(-beta);
}
board.UndoMove(move);
if (shouldStop)
	return 0;

if (score > bestScore)
{
	bestScore = score;
	bestMove = move;
	if (ply == 0)
		bestRootMove = move;
	if (score > alpha)
		alpha = score;
	if (alpha >= beta)
		break;
}
}

if (!isQSearch)
{
ttMoves[ttIndex] = bestMove.RawValue;
if (alpha >= beta && !bestMove.IsCapture && !bestMove.IsPromotion)
{
	killerMoves[ply] = bestMove;
	history[bestMove.RawValue & 4095 + (board.IsWhiteToMove ? 0 : 4096)] += depth * depth;
}
}

return bestScore;
}
}
}
using ChessChallenge.API;
using System;
using static ChessChallenge.API.BitboardHelper;

/*
* Features
*     - Evaluation
*         - Material Values
*         - Piece-Square tables(files f-h are mirrored from files a-d)
*         - Tapered Evaluation(interpolating from middle-game to end-game)
*     - Search
*         - Iterative Deepening
*         - Mate Distance Pruning
*         - Null move pruning
*         - Transposition Table
*             - Only stores moves to save tokens
*         - Move Ordering
*             - tt moves
*             - MVV_LVA(most valuable victim, least valuable attacker)
*             - Killer move heuristic
*         - Check extension
*         - Captures-only Quiescence Search
*     - Time management
*         - fixed allocation at start of search
*         - time allocated = time remaining / 40
*         - Check time every 2048 nodes to avoid overhead with invoking clock
*     - Initialization
*         - All static data is stored in a giant array of 64 bit unsigned integers
*         - Data is decoded using Buffer.BlockCopy
*/

using ChessChallenge.API;
using System;
using System.Linq;
using static ChessChallenge.API.BitboardHelper;

public class MyBotOld : IChessBot
{
	private Move bestMoveRoot;

	private int millisAlloced, nodes, phase, evalMG, evalEG, sq, it, psqtIdx;
	private bool shouldStop;
	//int nodes = 0;

	// TODO: Remove constants and replace them inline

	// 0-5 = middlegame material, 6-11 = endgame material, 12-17 = phase weights
	//short[] MAT_PHASE = new short[18];

	//sbyte[] HALF_PSQT = new sbyte[384];


	// pack static arrays into 64 bit unsigned integers, and then use Buffer.BlockCopy to extract them

	// these evaluation parameters were obtained from my engine, you can find them at https://github.com/mcthouacbb/Sirius
	// The values are in the main branch under the Sirius/src/eval in material.h, phase.h, and psqt.cpp
	// Additionally, the code for tuning the parameters is in the tune branch under Sirius/src/tune
	/*ulong[] ARRAYS_INIT =
	{
		104146991235792966,
		57421329040671514,
		3216957440225,
		5066618300661760,
		41,
        // PSQT
        5565390404019549171,
		575335840744079079,
		17651010900099003871,
		17578661999434596061,
		1313345551101969536,
		2386923248285596926,
		868076469175061239,
		16997416005460222958,
		146097452330450155,
		2239131215378520820,
		363111507762086647,
		17073694706158276095,
		4629726883482379546,
		722250380910925559,
		17216393684550741214,
		141272842907741643,
		17441283546016189948,
		17581748350570205199,
		17940376040718661368,
		284271008960805096,
		2815597404505391877,
		15773275864558155004,
		13532727398902125497,
		18367956423823589898,
		5718570421002364646,
		16495828885755736624,
		16564494391039814121,
		16638239752875207143,
		645424268913470170,
		2169063745724152050,
		1443405841412131836,
		18229129728889977834,
		18230567993061078260,
		651061529587940357,
		866660323917759486,
		18298402315792217327,
		18084769465568460546,
		18304035169612267009,
		18229160596669136894,
		18302068091720104446,
		2959450934766862586,
		2887126245924599534,
		146341673907066359,
		15340074901198791398,
		18377793707869464285,
		1447363017716796675,
		1589210978859747066,
		16497497759637960425,
	};*/

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
		// use 2.5% of remaining time(decent strategy that doesn't take up many tokens)
		millisAlloced = timer.MillisecondsRemaining / 40;
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
			//Console.WriteLine($"Mine Depth: {i - 1}, Move: {bestMoveRoot} eval: {eval}");
		}
		// this utilizes partial search results
		return bestMoveRoot;




		// use local function to access board and timer with no token overhead
		// idea from antares
		int Search(int depth, int alpha, int beta, bool doNull, int ply)
		{
			// local search function to save tokens, idea from Tyrant
			int LocalSearch(int localAlpha, int R = 1, bool localDoNull = true) => -Search(depth - R, localAlpha, -alpha, localDoNull, ply + 1);

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
				if ((nodes++ & 2047) == 0 && timer.MillisecondsElapsedThisTurn > millisAlloced || shouldStop)
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
				if (depth <= 6 && staticEval - depth * 70 >= beta)
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
					it = LocalSearch(-beta, 2 + depth / 3, false);
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
				board.MakeMove(move);

				bool isQuiet = !move.IsCapture && !move.IsPromotion;
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

				if (movesPlayed++ == 0 || isQSearch)
					score = LocalSearch(-beta);
				else if ((score = LocalSearch(-alpha - 1, reduction)) > alpha && (reduction > 1 || !notPV))
					score = LocalSearch(-beta);

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
						if (!isQSearch && !bestMove.IsCapture && !bestMove.IsPromotion)
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
