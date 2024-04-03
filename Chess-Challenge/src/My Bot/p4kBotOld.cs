//#define UCI_OUTPUT

using ChessChallenge.API;
using System.Linq;
using static System.Math;

public class P4kBotOld : IChessBot
{
#if UCI_OUTPUT
	ulong nodes;
#endif
	Move rootBestMove;
	Move[] ttMoves = new Move[16777216];
	public Move Think(Board board, Timer timer)
	{
		var (psqts, history) = (new[] {
			// extra padding, saves tokens in index calculation since PieceTypes start at 1
			0UL, 0x003c23120f0e0f00UL, 0x273b42403e3b3733UL, 0x333b3f3b3d3d3b35UL, 0x595a585653505051UL, 0xa7a7ada7a6a5a5a3UL, 0x7090706050201UL, 0xe151110100e110eUL, 0x292e333333312d26UL, 0x353a373838393833UL, 0x5054585a5b5a5855UL, 0xacacacababa9a7a3UL, 0x50c080806080700UL,

		}, new int[4096]);
		// putting search in here so we can use board without parameter(idea from antares)
		int Search(int depth, int alpha, int beta, bool root)
		{
			if (timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 4)
				depth /= 0;
			// multiplying bestScore by depth handles prioritizing shorter mates
			// bestScore will be set to eval in qsearch so this does not change anything for depth <= 0
			var (bestScore, score, eval, key, qsearch) = (-30000 * depth, 0, board.GetLegalMoves().Length, board.ZobristKey % 16777216, depth <= 0);
			// summoning demons by reusing local variables
			// score is a counter variable
			// Tuned material values were 977, 496, 335, 318, and 91
			// approximated values are 976, 492, 336, 320, and 96
			foreach (PieceList pieceList in board.GetAllPieceLists())
				foreach (Piece piece in pieceList)
				{
					// 3x quanitization
					// material baked into psqts
					eval +=
						((byte)(psqts[(int)piece.PieceType] >> (piece.Square.Rank ^ (pieceList.IsWhitePieceList ? 0 : 7)) * 8) +
						(byte)(psqts[(int)piece.PieceType + 6] >> piece.Square.File * 8)) * (pieceList.IsWhitePieceList == board.IsWhiteToMove ? 3 : -3);
				}
			/*eval += pieceList.Count *
				(1031623942 >> score++ * 6 % 36 & 63) *
				(pieceList.IsWhitePieceList == board.IsWhiteToMove ? 16 : -16);*/

			// uncomment this to test eval for a specific fen
			//if (root)
			//System.Console.WriteLine(eval);

			if (qsearch)
				// thanks to boychesser for this trick
				alpha = Max(alpha, bestScore = eval);
			else if (depth <= 6 && eval >= beta + 80 * depth)
				return eval;

			foreach (Move move in board.GetLegalMoves(qsearch).OrderByDescending(move => (ttMoves[key] == move, move.CapturePieceType, history[move.RawValue & 4095])))
			{
				if (bestScore >= beta || !qsearch && !move.IsCapture && eval + 120 * depth + 80 < alpha)
					break;
#if UCI_OUTPUT
				nodes++;
#endif
				board.MakeMove(move);
				score = board.IsDraw() ? 0 : -Search(board.IsInCheck() ? depth : depth - 1, -beta, -alpha, false);
				board.UndoMove(move);
				if (score > bestScore)
				{
					alpha = Max(alpha, bestScore = score);
					ttMoves[key] = move;
					if (root)
						rootBestMove = move;
					if (score >= beta && !move.IsCapture)
						history[move.RawValue & 4095] += depth * depth;
				}
			}
			return bestScore;
		}



		int depth = 0;
#if UCI_OUTPUT
		nodes = 0;
#endif
		// this is the most horrid bench implementation I have ever written, I wholeheartedly hope that no living creature
		// will ever be closer than 1000 lightyears to such abominations as I have written here.
#if UCI_OUTPUT
		if (timer.MillisecondsRemaining >= 100000000)
		{
			nodes = 0;
			int benchDepth = timer.OpponentMillisecondsRemaining;
			int startTime = timer.MillisecondsElapsedThisTurn;
			for (depth = 1; depth <= benchDepth; depth++)
			{
				Search(depth, -10000000, 10000000, true);
				//Console.WriteLine($"Mine Depth: {depth}, Move: {bestMoveRoot} eval: {eval}, nodes: {nodes}, alpha: {alpha}, beta: {beta}");
				//Console.WriteLine($"Timer: {timer.MillisecondsElapsedThisTurn}, t: {timer.MillisecondsRemaining / 15}");
				if (timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 50)
					break;
			}
			int endTime = timer.MillisecondsElapsedThisTurn;
			int time = endTime - startTime;

			System.Console.WriteLine($"Fen: {board.GetFenString()}, Bench Depth: {benchDepth}, nodes: {nodes}, time: {time}");
			return default;
		}
#endif
		try
		{
			while (timer.MillisecondsElapsedThisTurn < timer.MillisecondsRemaining / 30)
#if UCI_OUTPUT
			{
				int score =
#endif
				Search(++depth, -10000000, 10000000, true);
#if UCI_OUTPUT
				System.Console.WriteLine($"info depth {depth} score cp {score} time {timer.MillisecondsElapsedThisTurn} nodes {nodes} nps {nodes * 1000 / (ulong)Max(timer.MillisecondsElapsedThisTurn, 1)} pv {rootBestMove.ToString().Substring(7, rootBestMove.ToString().Length - 8)}");
			}
#endif
		}
		catch { }
		return rootBestMove;
	}
}