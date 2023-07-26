//#define DEBUG_TIMER
using ChessChallenge.API;
using System;
using System.Numerics;

public class AugsEUBot : IChessBot
{
	UInt64[] kWhitePTables = {
		 0xFF84E945B57084FF, 0x005ACA33883F3400, 0x002C0F45B5075000, 0xFF1ADE9D2230BEFF, 0xFFEA030426FE42FF, 0xFF9CB385A7BF9FFF, 0xFFA38385A7BF9FFF, 0x007F7C7A58406000,
		 0x328E81AEB4F0CA7D, 0x687A716DA856EA46, 0xD8E5775E36B06935, 0xC2D6C8A852C1D0D7, 0x2A4AB51983021E00, 0xF6E45A218383DDFB, 0x4CC321018383DFFE, 0x103CFEFE7C7C2000,
		 0xB092210E01314C47, 0xFCD118453650908E, 0x5FF76B7F896E1746, 0x6E14A5CA8821B9CC, 0xEE88FEFB9101D972, 0xD72D81C3810199BF, 0xFB8D81C3810199FF, 0x00727E3C7EFE6600,
		 0x52B5DBC241FE6719, 0x5A1C2AF403128CE5, 0x5A49D70C634EC2F8, 0xC8E1249DC93D1765, 0x8F9229666D715966, 0x103C41C7EEFE5EA7, 0x000001C7EFFFDFE7, 0xFFFFFE3810002018,
		 0x23566A2FC0D556E0, 0x8C28DC80EF63DA74, 0x15561E2FA29170B2, 0xE65B5EDC50AB9813, 0xE20E4DD0FDBEDA85, 0x13BDAFDFFFBFDB77, 0x031F0FDFFFBFDBF7, 0xFCE0F02000402408,
		 0x1414F8FB6CB7ACB2, 0xCB7517F201639FDF, 0x0CE928E98DC3A231, 0x4B47FEC33A5FBB51, 0x507A2584BB18971A, 0x68BEBD7F46E7A731, 0x79FEBDFFFFFFBF39, 0x86014200000040C6,
		 0xFF143BC864B426FF, 0x001FA406658A5700, 0x0002E5E81C278600, 0xFF2D293AC05B66FF, 0xFF6BE539FCFFE6FF, 0xFF57DD38FCFFE6FF, 0xFF7F0238FCFFE6FF, 0x007FFFC703001900,
		 0xF9A7C27F3EE2ABD0, 0x53B7E24208622788, 0x2B2500C1F72A086B, 0x4348D3DFC9C5F3BC, 0x869AB04242269D42, 0xBC7F73C3C3E77E3D, 0x7FFFF3C3C3E7FFFF, 0x00000C3C3C180000,
		 0x4D9166514B2681E8, 0x2BD703D5DEB66112, 0x51D6A00159C6D7E8, 0xC384FDC012A45BA5, 0x3C7FFFC1D3673C5A, 0xFFFFFFC1D3E7FFFF, 0xFFFFFFC1D3E7FFFF, 0x0000003E2C180000,
		 0xC0F9E802F7C6C794, 0x75F6A7BE4541B887, 0xCC50FF013E958350, 0xC0E01FFB0F8A0CCE, 0xC0F0FFFBFF7FFF7F, 0xC0F0FFFBFFFFFFFF, 0xC0F0FFFBFFFFFFFF, 0x3F0F000400000000,
		 0xBEFB53835EAB5DD7, 0xA17A71FDBC00C4E8, 0xB84B4A2057AF5812, 0x1EE11B0F9B897BCC, 0x018C3AA968884098, 0x01910351018BBF77, 0x01810301018BFFFF, 0xFE7EFCFEFE740000,
		 0x919E52AB9027D339, 0x0144B4852AD318A5, 0xEC3A1A581362E530, 0xCC4064DE3D59A4E4, 0x502160A182C26689, 0xDE01008183C3E77E, 0xDF01008183C3E7FF, 0x20FEFF7E7C3C1800
	};
	UInt64[] kBlackPTables;

	//                     .  P    K    B    R    Q    K
	int[] kPieceValues = { 0, 100, 300, 310, 500, 900, 10000 };
	int kMassiveNum = 99999999;

#if DEBUG_TIMER
	int dNumMovesMade = 0;
	int dTotalMsElapsed = 0;
#endif

	int mDepth;
	int mPhase = 0;
	Move mBestMove;

	public AugsEUBot()
	{
		kBlackPTables = new UInt64[kWhitePTables.Length];
		for (int i = 0; i < kWhitePTables.Length; ++i)
			kBlackPTables[i] = ReverseBits(kWhitePTables[i]);
	}

	public Move Think(Board board, Timer timer)
	{
		Move[] legalMoves = board.GetLegalMoves();
		mDepth = 6;

		if (board.PlyCount > 20)
			mPhase = 48;

		EvaluateBoardNegaMax(board, mDepth, -kMassiveNum, kMassiveNum, board.IsWhiteToMove ? 1 : -1);

#if DEBUG_TIMER
		dNumMovesMade++;
		dTotalMsElapsed += timer.MillisecondsElapsedThisTurn;
		Console.WriteLine("My bot time average: {0}", (float)dTotalMsElapsed / dNumMovesMade);
#endif
		return mBestMove;
	}

	int EvaluateBoardNegaMax(Board board, int depth, int alpha, int beta, int color)
	{
		Move[] legalMoves;

		if (board.IsDraw())
			return 0;

		if (depth == 0 || (legalMoves = board.GetLegalMoves()).Length == 0)
		{
			// EVALUATE

			if (board.IsInCheckmate())
				return -depth - 9999999;

			return color * (EvalColor(board, true) - EvalColor(board, false));
			// EVALUATE
		}

		// TREE SEARCH
		SortMoves(ref legalMoves);
		int recordEval = int.MinValue;
		foreach (Move move in legalMoves)
		{
			board.MakeMove(move);
			int evaluation = -EvaluateBoardNegaMax(board, depth - 1, -beta, -alpha, -color);
			board.UndoMove(move);

			if (recordEval < evaluation)
			{
				recordEval = evaluation;
				if (depth == mDepth)
					mBestMove = move;
			}
			alpha = Math.Max(alpha, recordEval);
			if (alpha >= beta) break;
		}
		// TREE SEARCH

		return recordEval;
	}

	void SortMoves(ref Move[] moves)
	{
		Move temp;
		for (int i = 1, j = 0; i < moves.Length; ++i)
		{
			if (moves[i].IsCapture || moves[i].IsPromotion)
			{
				temp = moves[j];
				moves[j++] = moves[i];
				moves[i] = temp;
			}
		}
	}

	int EvalColor(Board board, bool isWhite)
	{
		UInt64[] PTable = isWhite ? kWhitePTables : kBlackPTables;
		int sum = 0;
		for (int i = 1; i < 7; ++i)
		{
			ulong pieceBitBoard = board.GetPieceBitboard((PieceType)i, isWhite);
			sum += (kPieceValues[i] - 121) * BitOperations.PopCount(pieceBitBoard);
			for (int b = 0; b < 8; ++b)
				sum += BitOperations.PopCount(pieceBitBoard & PTable[(i - 1) * 8 + b + mPhase]) * (1 << b);
		}
		return sum;
	}

	public static UInt64 ReverseBits(UInt64 num)
	{
		num = ((num & 0x5555555555555555) << 1) | ((num >> 1) & 0x5555555555555555);
		num = ((num & 0x3333333333333333) << 2) | ((num >> 2) & 0x3333333333333333);
		num = ((num & 0x0F0F0F0F0F0F0F0F) << 4) | ((num >> 4) & 0x0F0F0F0F0F0F0F0F);
		num = ((num & 0x00FF00FF00FF00FF) << 8) | ((num >> 8) & 0x00FF00FF00FF00FF);
		num = ((num & 0x0000FFFF0000FFFF) << 16) | ((num >> 16) & 0x0000FFFF0000FFFF);
		num = (num << 32) | (num >> 32);

		return num;
	}
}