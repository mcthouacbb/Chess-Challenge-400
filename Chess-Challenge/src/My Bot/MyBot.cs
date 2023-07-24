using ChessChallenge.API;
using static ChessChallenge.Chess.BitBoardUtility;
using System;

public class MyBot : IChessBot
{
    private Move bestMove;
    private Board board;
    private Timer timer;

    private int ply, millisAlloced;
    private bool shouldStop;
    //int nodes = 0;

    // TODO: Remove constants and replace them inline

    // 0-5 = middlegame material, 6-11 = endgame material, 12-17 = phase weights
    short[] MAT_PHASE = new short[18];

	sbyte[] HALF_PSQT = new sbyte[384], MVV_LVA = new sbyte[36];


    // pack static arrays into 64 bit unsigned integers, and then use Buffer.BlockCopy to extract them

    // these evaluation parameters were obtained from my engine, you can find them at https://github.com/mcthouacbb/Sirius
    // The values are in the main branch under the Sirius/src/eval in material.h, phase.h, and psqt.cpp
    // Additionally, the code for tuning the parameters is in the tune branch under Sirius/src/tune
    ulong[] ARRAYS_INIT = new ulong[]
	{
        // MVV_LVA
		1733323143564695823, 3107790562467916834, 14683016086159413, 1443966850258900235,
        // first 4 bytes is MVV_LVA, last 4 bytes is MAT_PHASE
        74591169479321630,
        // MAT_PHASE 2-17
        3410228281635,
        115687181809090661,
        1688849860264685,
        176094838800,
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
    int[] PSQT = new int[768], moveScores = new int[256];

    Move[,] killerMoves = new Move[128,2];

    Move[] ttMoves = new Move[33554432];

    void sortMoves(Move[] moves)
    {
        Move hashMove = ttMoves[board.ZobristKey % 33554432];

        for (int i = 0; i < moves.Length; i++)
        {
            int score = -1000000;
            if (moves[i] == hashMove)
                score = 1000000;
            else if (moves[i].IsCapture)
                score = MVV_LVA[
                    6 * (int)moves[i].MovePieceType + (int)moves[i].CapturePieceType - 7
                ];
            else if (moves[i] == killerMoves[ply, 0] || moves[i] == killerMoves[ply, 1])
                score = -100;

            // negate score to invert comparison and sort in descending order without using expensive comparator(token-wise)
            moveScores[i] = -score;
        }

        Array.Sort(moveScores, moves, 0, moves.Length);
    }

    public MyBot()
    {
        // using a constructor is probably a hacky way to initialize the tables, but I can't think of any other way
        Buffer.BlockCopy(ARRAYS_INIT, 0, MVV_LVA, 0, 36);
        Buffer.BlockCopy(ARRAYS_INIT, 36, MAT_PHASE, 0, 36);
        Buffer.BlockCopy(ARRAYS_INIT, 72, HALF_PSQT, 0, 384);
        // dumb hack to get rid of braces, don't do this in real code
        for (int i = 0; i < 384; i += 4)
            for (int j = 0; j < 4; j++)
                PSQT[2 * i + j] = PSQT[2 * i + 7 - j] = HALF_PSQT[i + j];

        /*for (int i = 0; i < 6; i++)
        {
            Console.Write("{");
            for (int j = 0; j < 6; j++)
            {
                Console.Write($"{MVV_LVA[6 * i + j]}, ");
            }
            Console.WriteLine("}");
        }

        for (int i = 0; i < 3; i++)
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
        ply = 0;
        //nodes = 0;

		this.board = board;
        this.timer = timer;

        // in case the search aborts on the first
        Move move = Move.NullMove;
        for (int i = 1; i < 128; i++)
        {
            Search(i, -200000, 200000, false);
            if (shouldStop)
                break;
            // if this depth takes up more than 50% of allocated time, there is a good chance that the next search won't finish.
            if (timer.MillisecondsElapsedThisTurn > millisAlloced / 2)
                break;
            //Console.WriteLine($"Mine Depth: {i}, Move: {bestMove} eval: {eval} nodes: {nodes}");
            move = bestMove;
        }
        return move;
    }

    int evaluate()
	{
        int phase = 242, evalMG = 0, evalEG = 0;

		for (int i = 0; i < 6; i++)
		{
			ulong whiteBB = board.GetPieceBitboard((PieceType)(i + 1), true),
				blackBB = board.GetPieceBitboard((PieceType)(i + 1), false);

            while (whiteBB != 0)
			{
				int sq = PopLSB(ref whiteBB);
				evalMG += PSQT[i * 64 + (sq ^ 0b111000)] + MAT_PHASE[i];
				evalEG += PSQT[384 + i * 64 + (sq ^ 0b111000)] + MAT_PHASE[6 + i];
				phase -= MAT_PHASE[12 + i];
			}

			while (blackBB != 0)
			{
				int sq = PopLSB(ref blackBB);
				evalMG -= PSQT[i * 64 + sq] + MAT_PHASE[i];
				evalEG -= PSQT[384 + i * 64 + sq] + MAT_PHASE[6 + i];
				phase -= MAT_PHASE[12 + i];
			}
		}

        return (evalMG * (242 - phase) + evalEG * phase) / (board.IsWhiteToMove ? 242 : -242);
    }

    int Search(int depth, int alpha, int beta, bool doNull)
    {
        if (timer.MillisecondsElapsedThisTurn > millisAlloced || shouldStop)
        {
            shouldStop = true;
            return alpha;
        }

        // if we already found a faster mate, no need to search deeper
        alpha = Math.Max(alpha, ply - 32000);
		beta = Math.Min(beta, 32000 - ply);
		// max ply is 127
		if (alpha >= beta || ply >= 127)
			return alpha;

		if (board.IsDraw())
            return 0;

        if (depth <= 0)
            return QSearch(alpha, beta);

        // null move pruning + another dumb hack for braces
        if (doNull && !board.IsInCheck())
            // ensure there is at least 1 non-pawn pieces on the board to avoid common zugzwang cases
            if ((
                    (board.IsWhiteToMove ? board.WhitePiecesBitboard : board.BlackPiecesBitboard) ^
                    board.GetPieceBitboard(PieceType.Pawn, board.IsWhiteToMove))
                != 0 && depth >= 3)
            {
                // already checked for checks, can't fail
                board.TrySkipTurn();
                int nullScore = -Search(depth - 3, -beta, -beta + 1, false);
                board.UndoSkipTurn();
                if (nullScore >= beta)
                {
                    return beta;
                }
            }

        var moves = board.GetLegalMoves();

        // board.IsDraw() already detects stalemate, so no need to check for it again
        if (moves.Length == 0)
            return ply - 32000;

        sortMoves(moves);


        Move best = Move.NullMove;
        foreach (Move move in moves)
        {
            board.MakeMove(move);
            ply++;
            // basic check extension
            int score = -Search(depth - (board.IsInCheck() ? 0 : 1), -beta, -alpha, true);
            ply--;
            board.UndoMove(move);
			if (shouldStop)
				return alpha;

			if (score >= beta)
            {
                if (move != killerMoves[ply, 0] && move.CapturePieceType == PieceType.None)
                {
                    killerMoves[ply, 1] = killerMoves[ply, 0];
                    killerMoves[ply, 0] = move;
                }
                ttMoves[board.ZobristKey % 33554432] = move;
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

        ttMoves[board.ZobristKey % 33554432] = best;

        return alpha;
    }

    int QSearch(int alpha, int beta)
    {
        int eval = evaluate();
        if (eval >= beta)
            return beta;
        if (eval > alpha)
            alpha = eval;
        if (ply >= 128)
            return alpha;

        var captures = board.GetLegalMoves(true);
        sortMoves(captures);

        foreach(Move capture in captures)
        {
            board.MakeMove(capture);
            int score = -QSearch(-beta, -alpha);
            board.UndoMove(capture);

            if (score >= beta)
                return beta;
            if (score > alpha)
                alpha = score;
        }
        return alpha;
    }
}