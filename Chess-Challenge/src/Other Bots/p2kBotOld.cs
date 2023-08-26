using ChessChallenge.API;
using System.Linq;

public class P2kBotOld : IChessBot
{
	Move bestMove;
	public Move Think(Board board, Timer timer)
	{
		// putting search in here so we can use board without parameter(idea from antares)
		int Search(int depth, int alpha, int beta, bool root)
		{
			if (board.IsDraw())
				return 0;
			// multiply bestScore by depth handles both prioritizing shorter mates and setting it to zero for evaluation at depth 0
			int bestScore = -30000 * depth,
				score;
			if (depth == 0)
			{
				// summoning demons by reusing local variables
				// bestScore is a counter variable
				// depth accumulates eval
				// bestScore is 0 because it is multiplied by depth, and depth is 0
				// Tuned material values were 977, 496, 335, 318, and 91
				// approximated values are 976, 492, 336, 320, and 96
				foreach (PieceList pieceList in board.GetAllPieceLists())
					depth += pieceList.Count *
						(1031623942 >> bestScore++ * 6 % 36 & 63) *
						(pieceList.IsWhitePieceList == board.IsWhiteToMove ? 16 : -16);
				return depth + board.GetLegalMoves().Length;
			}

			foreach (Move move in board.GetLegalMoves().OrderByDescending(move => move.CapturePieceType))
			{
				board.MakeMove(move);
				score = -Search(depth - 1, -beta, -alpha, false);
				board.UndoMove(move);
				if (score > bestScore)
				{
					bestScore = score;
					if (root)
						bestMove = move;
				}
				if (bestScore > alpha)
					alpha = bestScore;
				if (alpha >= beta)
					break;
			}
			return bestScore;
		}

		Search((int)System.Math.Log10(timer.MillisecondsRemaining * 66.6666), -10000000, 10000000, true);
		return bestMove;
	}
}