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

public class MyBotOld : IChessBot
{
    private Move bestMove;
    private Board board;
    private Timer timer;

    private int ply, millisAlloced, nodes, phase, evalMG, evalEG, sq, it, psqtIdx;
    private bool shouldStop;
    //int nodes = 0;

    // TODO: Remove constants and replace them inline

    // 0-5 = middlegame material, 6-11 = endgame material, 12-17 = phase weights
    short[] MAT_PHASE = new short[18];

    sbyte[] HALF_PSQT = new sbyte[384];


    // pack static arrays into 64 bit unsigned integers, and then use Buffer.BlockCopy to extract them

    // these evaluation parameters were obtained from my engine, you can find them at https://github.com/mcthouacbb/Sirius
    // The values are in the main branch under the Sirius/src/eval in material.h, phase.h, and psqt.cpp
    // Additionally, the code for tuning the parameters is in the tune branch under Sirius/src/tune
    ulong[] ARRAYS_INIT =
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
    };

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
    int[] PSQT = new int[768], history = new int[8192];

    Move[,] killerMoves = new Move[128, 2];

    // because of token shortages, only TT moves are stored
    ushort[] ttMoves = new ushort[67108864];

    ulong ttIndex
    {
        get
        {
            return board.ZobristKey % 67108864;
        }
    }

    public MyBotOld()
    {
        // using a constructor is probably a hacky way to initialize the tables, but I can't think of any other way
        Buffer.BlockCopy(ARRAYS_INIT, 0, MAT_PHASE, 0, 36);
        Buffer.BlockCopy(ARRAYS_INIT, 40, HALF_PSQT, 0, 384);

        // reusing other variables to reduce tokens: please do not summon these kinds of demons in real code
        for (evalMG = 0; evalMG < 384; evalMG += 4)
            for (evalEG = 0; evalEG < 4; evalEG++)
                PSQT[2 * evalMG + evalEG] = PSQT[2 * evalMG + 7 - evalEG] = HALF_PSQT[evalMG + evalEG];

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
        ply = nodes = 0;

        this.board = board;
        this.timer = timer;

        // this is important
        Array.Clear(history);

        for (int i = 1; i < 128;)
        {
            Search(i++, -200000, 200000, false);
            // if this depth takes up more than 50% of allocated time, there is a good chance that the next search won't finish.
            //if (shouldStop || timer.MillisecondsElapsedThisTurn > millisAlloced / 2)
            //break;
            //Console.WriteLine(nodes);
            //Console.ForegroundColor = ConsoleColor.Green;
            //Console.WriteLine($"Mine Depth: {i - 1}, Move: {bestMove} eval: {eval}");
        }
        return bestMove;
    }

    int evaluate()
    {
        phase = evalMG = evalEG = it = 0;

        // incremented on line 267
        for (; it < 6; it++)
        {
            foreach (bool stm in new bool[] { true, false })
            {
                ulong pieceBB = board.GetPieceBitboard((PieceType)(it + 1), stm);
                int sign = stm ? 1 : -1;
                //blackBB = board.GetPieceBitboard((PieceType)(it + 1), false);

                while (pieceBB != 0)
                {
                    sq = ClearAndGetIndexOfLSB(ref pieceBB) ^ (stm ? 0b111000 : 0);
                    evalMG += sign * (PSQT[psqtIdx = it * 64 + sq] + MAT_PHASE[it]);
                    evalEG += sign * (PSQT[384 + psqtIdx] + MAT_PHASE[6 + it]);
                    phase += MAT_PHASE[12 + it];
                }
            }
        }
        // linearly interpolate between evalMG and evalEG using phase(0-242)
        // TODO: check if multiplying endgame eval by (100 - halfMoveClock) / 100 helps with avoiding endgame draws
        return (evalMG * phase + evalEG * (242 - phase)) / (board.IsWhiteToMove ? 242 : -242);
    }

    int Search(int depth, int alpha, int beta, bool doNull)
    {
        bool isQSearch = depth <= 0;
        if ((nodes++ & 2047) == 0 && timer.MillisecondsElapsedThisTurn > millisAlloced || shouldStop)
        {
            shouldStop = true;
            return alpha;
        }

        int eval = evaluate();
        if (isQSearch)
        {
            if (eval >= beta)
                return beta;
            // delta pruning
            if (eval < alpha - 900)
                return alpha;
            if (eval > alpha)
                alpha = eval;
        }
        else
        {
            if (board.IsInsufficientMaterial() || board.IsRepeatedPosition() || board.FiftyMoveCounter >= 100)
                return 0;

            if (depth <= 6 && eval - depth * 70 >= beta)
                return beta;

            // null move pruning
            /*
             * Make a "null move" and see if we can get a fail high
             * Disabled if:
             *     - In Check, position after null move will be illegal
             *     - Null move was done last move. Double null move just burns 2 plies for nothing
             *     - Depth is less than reduction factor
             *     - Only pawns left for side to move, zugzwang becomes extremely common
             */
            if (doNull && depth >= 3 && GetNumberOfSetBits((board.IsWhiteToMove ? board.WhitePiecesBitboard : board.BlackPiecesBitboard) ^
                board.GetPieceBitboard(PieceType.Pawn, board.IsWhiteToMove)) >= 2 && board.TrySkipTurn())
            {
                int nullScore = -Search(depth - 3, -beta, -beta + 1, false);
                board.UndoSkipTurn();
                if (nullScore >= beta)
                    return beta;
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
                moves[i] == killerMoves[ply, 0] || moves[i] == killerMoves[ply, 1] ? 100 :
                2000000000 - history[moves[i].RawValue & 4095 + (board.IsWhiteToMove ? 0 : 4096)];

        MemoryExtensions.Sort(moveScores, moves);

        Move best = Move.NullMove;
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            ply++;
            // check extension, don't decrement depth if in check after make move
            int score = -Search(depth - (board.IsInCheck() ? 0 : 1), -beta, -alpha, true);
            ply--;
            board.UndoMove(move);
            if (shouldStop)
                return alpha;

            if (score >= beta)
            {
                if (!isQSearch)
                {
                    if (!move.IsCapture && !move.IsPromotion)
                    {
                        if (move != killerMoves[ply, 0])
                        {
                            killerMoves[ply, 1] = killerMoves[ply, 0];
                            killerMoves[ply, 0] = move;
                        }
                        history[move.RawValue & 4095 + (board.IsWhiteToMove ? 0 : 4096)] += depth * depth;
                    }
                    ttMoves[ttIndex] = move.RawValue;
                }
                return beta;
            }

            if (score > alpha)
            {
                alpha = score;
                best = move;
                if (ply == 0)
                    bestMove = move;
            }
        }

        if (!isQSearch)
            ttMoves[ttIndex] = best.RawValue;

        return alpha;
    }
}
