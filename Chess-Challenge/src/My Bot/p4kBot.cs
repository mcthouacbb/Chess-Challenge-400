#define UCI_OUTPUT

using ChessChallenge.API;
using System.Linq;
using static System.Math;

public class P4kBot : IChessBot
{
#if UCI_OUTPUT
	ulong nodes;
#endif
	Move rootBestMove;
	Move[] ttMoves = new Move[16777216];
	public Move Think(Board board, Timer timer)
	{
		var (psqts, history, depth) = (new[] {
			0x3723130f0e0f00UL, 0x283a42413c37322bUL, 0x363b41403e3d3a34UL, 0x5f605e5a55525152UL, 0xadafb3afaba9a7a4UL, 0xd110e09050302UL, 0xe161111100f120fUL, 0x2d32363636332f28UL, 0x3539383838383733UL, 0x5159595b5c5b5855UL, 0xadacacababaaa7a4UL, 0x408040405070700UL,
		}, new int[4096], 0);
		// putting search in here so we can use board without parameter(idea from antares)
		int Search(int depth, int alpha, int beta, bool root)
		{
			// subtracting depth from bestScore handles prioritizing shorter mates
			// bestScore will be set to eval in qsearch so this does not change anything for depth <= 0
			if (board.IsInCheck())
				depth++;
			var (bestScore, score, eval, key, qsearch, movesTried) = (-30000 - depth, 0, 21, board.ZobristKey % 16777216, depth <= 0, 10);
			// summoning demons by reusing local variables
			// score is a counter variable
			// Tuned material values were 977, 496, 335, 318, and 91
			// approximated values are 976, 492, 336, 320, and 96
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

			// uncomment this to test eval for a specific fen
			//if (root)
			//System.Console.WriteLine(eval);

			if (qsearch || depth <= 4 && !board.IsInCheck() && eval >= beta + 80 * depth)
				// thanks to boychesser for this trick
				alpha = Max(alpha, bestScore = eval);
			// one could remove this else if and merge the condition into the previous
			// but this causes an extra move generation to be done on rfp which seems to slow down the engine by a lot
			// I won't do this unless I'm sure I can make those tokens worth it

			if (bestScore >= beta)
				return eval;

			foreach (Move move in board.GetLegalMoves(qsearch).OrderByDescending(move => (ttMoves[key] == move, move.CapturePieceType, history[move.RawValue & 4095])))
			{
#if UCI_OUTPUT
				nodes++;
#endif
				board.MakeMove(move);
				bool notReduce = movesTried++ <= 20 || depth <= 3;
				do
					score = board.IsDraw() ? 0 : -Search(depth - (notReduce ? 1 : movesTried / 10), -beta, -alpha, false);
				while (score > alpha && (notReduce = !notReduce));

				board.UndoMove(move);

				if (timer.MillisecondsElapsedThisTurn > timer.MillisecondsRemaining / 4)
					return 0;

				if (score > bestScore)
				{
					alpha = Max(alpha, bestScore = score);
					ttMoves[key] = move;
					if (root)
						rootBestMove = move;
				}
				if (score >= beta)
				{
					if (!move.IsCapture)
						history[move.RawValue & 4095] += depth * depth;
					break;
				}
			}
			return bestScore;
		}
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
			for (depth = 1;  depth <= benchDepth; depth++)
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
		return rootBestMove;
	}
}