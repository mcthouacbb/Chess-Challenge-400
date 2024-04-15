using ChessChallenge.API;
using System.Linq;
// this using saves tokens
using static System.Math;

public class AtadOfANoobBot : IChessBot
{
	Move rootBestMove;
	Move[] ttMoves = new Move[16777216];
	public Move Think(Board board, Timer timer)
	{
		// 8 bit 3x quantized rank file psqts are packed into ulongs
		// pawn rank, knight rank, bishop rank ... king rank, pawn file, knight file ... king file

		// history indexed by from-to
		var (psqts, history, depth) = (new[] {
			0x3723130f0e0f00UL, 0x283a42413c37322bUL, 0x363b41403e3d3a34UL, 0x5f605e5a55525152UL, 0xadafb3afaba9a7a4UL, 0xd110e09050302UL, 0xe161111100f120fUL, 0x2d32363636332f28UL, 0x3539383838383733UL, 0x5159595b5c5b5855UL, 0xadacacababaaa7a4UL, 0x408040405070700UL,
		}, new int[4096], 0);
		// putting search in here so we can use board without parameter(idea from antares)
		int Search(int depth, int alpha, int beta, bool root)
		{
			// check extensions
			if (board.IsInCheck())
				depth++;

			// subtracing depth handles prioritizing shorter mates
			// movesTriedPlusTen is the number of moves tried + 10(+ 10 because you don't have to do + 1 for lmr
			var (bestScore, score, eval, key, qsearch, movesTriedPlusTen) = (-30000 - depth, 0, 21, board.ZobristKey % 16777216, depth <= 0, 10);

			// summoning demons by reusing local variables
			// score is a counter variable
			// order of piece lists is always white(pnbrqk), black(pnbrqk)
			// so we can take advantage of this by using score to access the psqts
			foreach (PieceList pieceList in board.GetAllPieceLists())
			{
				foreach (Piece piece in pieceList)
					// 3x quanitization
					// material baked into psqts
					eval +=
						((byte)(psqts[score % 6] >> (piece.Square.Rank ^ score / 6 * 7) * 8) +
						(byte)(psqts[score % 6 + 6] >> piece.Square.File * 8)) * (pieceList.IsWhitePieceList == board.IsWhiteToMove ? 3 : -3);
				score++;
			}

			// stand pat + rfp
			if (qsearch || depth <= 4 && !board.IsInCheck() && eval >= beta + 80 * depth)
				// thanks to boychesser for this trick
				alpha = Max(alpha, bestScore = eval);

			// stand pat beta cutoff + rfp
			if (bestScore >= beta)
				return eval;

			// tt move ordering + mvv ordering(no lva) + history
			// thanks to CJ for showing me this tuple ordering trick
			foreach (Move move in board.GetLegalMoves(qsearch).OrderByDescending(move => (ttMoves[key] == move, move.CapturePieceType, history[move.RawValue & 4095])))
			{
				board.MakeMove(move);
				// reduce when movesTried > 10 and depth > 3
				bool notReduce = movesTriedPlusTen++ <= 20 || depth <= 3;

				// this crazy do while loop does the reduced depth search and the lmr research
				// due to the token limit there is no pvs
				// the reduced depth search and the research are both done with full windows
				do
					score = board.IsDraw() ? 0 : -Search(depth - (notReduce ? 1 : movesTriedPlusTen / 10), -beta, -alpha, false);
				while (score > alpha && (notReduce = !notReduce));

				board.UndoMove(move);
				// hard time check
				if (timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 4)
					return 0;

				// update stuff
				if (score > bestScore)
				{
					alpha = Max(alpha, bestScore = score);
					ttMoves[key] = move;
					if (root)
						rootBestMove = move;
				}
				// fail high
				if (score >= beta)
				{
					// update the history when a quiet move fails high
					if (!move.IsCapture)
						history[move.RawValue & 4095] += depth * depth;
					break;
				}
			}

			return bestScore;
		}

		// soft time check
		while (timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 30)
				Search(++depth, -10000000, 10000000, true);

		return rootBestMove;
	}
}