using ChessChallenge.API;
using static ChessChallenge.Chess.BitBoardUtility;
using System;
using System.Runtime.CompilerServices;
using System.Transactions;

public class MyBot : IChessBot
{
    private Move bestMove;
    private Board board;

    private int ply;
    //int nodes = 0;

    // TODO: Remove constants and replace them inline

    // 0-5 = middlegame material, 6-11 = endgame material, 12-17 = phase weights
    short[] MAT_PHASE = new short[18];

    /*{
        70, 265, 291, 370, 794, 0
    },
        MATERIAL_EG = new int[]
    {
        101, 204, 225, 411, 749, 0
    },
        PHASES = new int[]
    {
        // pawn - queen
        // extra zeroes are so that calculations can be packed into a single for loop with PSQT, rather than multiple loops
        0, 6, 16, 18, 41, 0
    };*/

    byte[] MVV_LVA = new byte[36];
    ulong[] ARRAYS_INIT = new ulong[]
	{
        // MVV_LVA
		1733323143564695823, 3107790562467916834, 14683016086159413, 1443966850258900235, 3287070,
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

    sbyte[] HALF_PSQT = new sbyte[384];
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
    int[] PSQT = new int[768];

    Move[,] killerMoves = new Move[128,2];

    void sortMoves(Move[] moves)
    {
        var scores = new int[moves.Length];

        for (int i = 0; i < moves.Length; i++)
        {
            int score = -1000000;
            if (moves[i].IsCapture)
                score += MVV_LVA[
                    6 * (int)moves[i].MovePieceType + (int)moves[i].CapturePieceType - 7
                ];
            else if (moves[i] == killerMoves[ply, 0] || moves[i] == killerMoves[ply, 1])
                score = -100;

            scores[i] = -score;
        }

        Array.Sort(scores, moves);
    }

    public MyBot()
    {
        Buffer.BlockCopy(ARRAYS_INIT, 0, MVV_LVA, 0, 36);
        Buffer.BlockCopy(ARRAYS_INIT, 40, HALF_PSQT, 0, 384);
        // dumb hack to get rid of braces, don't do this in real code
        for (int i = 0; i < 384; i += 4)
            for (int j = 0; j < 4; j++)
            {
                PSQT[2 * i + j] = HALF_PSQT[i + j];
                PSQT[2 * i + 7 - j] = HALF_PSQT[i + j];
            }
    }

    public Move Think(Board board, Timer timer)
    {
        ply = 0;
        //nodes = 0;

		this.board = board;

		Search(6, -200000, 200000, false);
        //Console.WriteLine($"Nodes: {nodes}");
        return bestMove;
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
				evalMG += PSQT[i * 64 + (sq ^ 0b111000)];
				evalEG += PSQT[384 + i * 64 + (sq ^ 0b111000)];
				evalMG += MAT_PHASE[i];
				evalEG += MAT_PHASE[6 + i];
				phase -= MAT_PHASE[12 + i];
			}

			while (blackBB != 0)
			{
				int sq = PopLSB(ref blackBB);
				evalMG -= PSQT[i * 64 + sq];
				evalEG -= PSQT[384 + i * 64 + sq];
				evalMG -= MAT_PHASE[i];
				evalEG -= MAT_PHASE[6 + i];
				phase -= MAT_PHASE[12 + i];
			}
		}

		int phaseFactor = (phase * 256 + 121) / 242;
        return (evalMG * (256 - phaseFactor) + evalEG * phaseFactor) / (board.IsWhiteToMove ? 256 : -256);
    }

    int Search(int depth, int alpha, int beta, bool doNull)
    {
        //nodes++;

        // if we already found a better mate, no need to search deeper
		alpha = Math.Max(alpha, ply - 32000);
		beta = Math.Min(beta, 32000 - ply);
		if (alpha >= beta)
			return alpha;

		if (board.IsDraw())
            return 0;

        if (depth <= 0)
            return QSearch(alpha, beta);

        // max ply is 127
        if (ply >= 127)
            return alpha;

        // TODO: Transposition table + Better Move Ordering

        // null move pruning
        if (doNull && !board.IsInCheck())
        {
            // ensure there is at least one non-pawn piece on the board to avoid common zugzwang cases
            ulong nonPawns = (board.IsWhiteToMove ? board.WhitePiecesBitboard : board.BlackPiecesBitboard) ^ board.GetPieceBitboard(PieceType.Pawn, board.IsWhiteToMove);
            if ((nonPawns & (nonPawns - 1)) != 0 && depth >= 3)
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
        }


        var moves = board.GetLegalMoves();

        // board.IsDraw() already detects stalemate, so no need to check for it again
        if (moves.Length == 0)
            return ply - 32000;

        sortMoves(moves);


        foreach (Move move in moves)
        {
            board.MakeMove(move);
            ply++;
            // basic check extension
            int score = -Search(depth - (board.IsInCheck() ? 0 : 1), -beta, -alpha, true);
            ply--;
            board.UndoMove(move);

            if (score >= beta)
            {
                if (move != killerMoves[ply, 0] && move.CapturePieceType == PieceType.None)
                {
                    killerMoves[ply, 1] = killerMoves[ply, 0];
                    killerMoves[ply, 0] = move;
                }
                return beta;
            }

            if (score > alpha)
            {
                alpha = score;
                if (ply == 0)
                    bestMove = move;
            }
        }

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
            //nodes++;
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