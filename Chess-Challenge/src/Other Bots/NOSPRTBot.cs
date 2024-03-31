/*
NOSPRT - a 400 token engine by cj5716
Features:
   - Iterative Deepening
   - Negamax with AB pruning
   - Quiescence Search
   - Hard and soft bounds for time management
   - TT moves
   - MVV-LVA
   - Rank and file PSTs
   - Mobility evaluation
   - Reverse Futility Pruning
   - Null Move Pruning
*/

using System;
using System.Linq;
using ChessChallenge.API;

public class NOSPRTBot : IChessBot
{
	// Root best move
	Move rootBestMove;

	// TT moves
	Move[] TT = new Move[8388608];

	public Move Think(Board board, Timer timer)
	{
		int globalDepth = 0;

		long nodes = 0; // #DEBUG

		int Search(int depth, int alpha, int beta)
		{
			// Assign zobrist key
			// Set a variable that indicates if we are in qsearch
			// Score is init to tempo value of 15
			// Eval terms packed into ulongs (8 bytes per value)
			var (key,
				 inQsearch,
				 score,
				 evalValues) =
				(board.ZobristKey % 8388608,
				 depth <= 0,
				 15,
				 new[] { 0x0001030405000000ul, 0x001E110704040400ul, 0x1F25292825221F1Cul, 0x2424272725242421ul, 0x323332302C29282Aul, 0x6A65686663626262ul, 0x0000100000FC0000ul, 0x0E1211100F0F100Ful, 0x1B1E2021211F1C17ul, 0x1C1D1C1D1D1D1D1Bul, 0x363A3A3A3B3A3937ul, 0x6B69686767676563ul, 0xFAFC0405FA0E0101ul });

			// Extract function to extract values from ulong
			/*
            sbyte Extract(ulong term, int index) => (sbyte)(term >> index * 8);
            */
			// Currently, this is inlined into eval to save tokens

			foreach (bool isWhite in new[] { !board.IsWhiteToMove, board.IsWhiteToMove })
			{
				score = -score;
				ulong bitboard = isWhite ? board.WhitePiecesBitboard : board.BlackPiecesBitboard,
					  sideBB = bitboard;

				while (bitboard != 0)
				{
					int sq = BitboardHelper.ClearAndGetIndexOfLSB(ref bitboard),
						pieceIndex = (int)board.GetPiece(new(sq)).PieceType;

					// Mobility, we use the raw value instead of evalValues[0] because it is smaller
					score += (sbyte)(0x0001030405000000ul >> pieceIndex * 8) * BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks((PieceType)pieceIndex, new(sq), board, isWhite) & ~sideBB)

						   // 6x quantization, rank and file PSTs  (~20 Elo off full PSTs)
						   // Material is encoded within the PSTs
						   // We inline flipping the square if it is black
						   + ((sbyte)(evalValues[pieceIndex] >> ((sq ^= isWhite ? 0 : 56) & 0b111000))
						   + (sbyte)(evalValues[pieceIndex + 6] >> sq % 8 * 8)) * 6;

					// Code above is quivalent to:
					/*
                    score += Extract(0x0001030405000000ul, pieceIndex) * BitboardHelper.GetNumberOfSetBits(BitboardHelper.GetPieceAttacks((PieceType)pieceIndex, new (sq), board, isWhite) & ~sideBB);
                    sq ^= isWhite ? 0 : 56;
                    score += (Extract(evalValues[pieceIndex], sq / 8)
                           +  Extract(evalValues[pieceIndex + 6], sq % 8)) * 6;
                    */

				}
			}

			if (board.TrySkipTurn())
			{
				score = inQsearch ? alpha = Math.Max(score, alpha)
					  : depth <= 4 ? score - 100 * depth
								   : -Search(depth - 4, -beta, -beta + 1);
				board.UndoSkipTurn();
			}
			else
				score = alpha;

			// Loop over each legal move
			// TT move then MVV-LVA
			foreach (var move in board.GetLegalMoves(inQsearch).OrderByDescending(move => (move == TT[key], move.CapturePieceType, 0 - move.MovePieceType)))
			{
				if (score >= beta)
					return beta;

				board.MakeMove(move);
				nodes++; // #DEBUG

				score = board.IsInCheckmate() ? 1_000_000 - board.PlyCount
					  : board.IsDraw() ? 0
											  : -Search(depth - 1, -beta, -alpha);

				board.UndoMove(move);

				if (score > alpha)
				{
					TT[key] = move;
					if (depth == globalDepth) rootBestMove = move;
					alpha = score;
				}

				Convert.ToUInt32(timer.MillisecondsRemaining - timer.MillisecondsElapsedThisTurn * 8);
			}

			return alpha;
		}

		try
		{
			// Iterative deepening, soft time limit
			while (timer.MillisecondsElapsedThisTurn <= timer.MillisecondsRemaining / 40)
			{ // #DEBUG
				int score = // #DEBUG
				Search(++globalDepth, -2_000_000, 2_000_000);

				var elapsed = timer.MillisecondsElapsedThisTurn > 0 ? timer.MillisecondsElapsedThisTurn : 1; // #DEBUG
				Console.WriteLine($"info depth {globalDepth} " + // #DEBUG
								  $"score cp {score} " + // #DEBUG
								  $"time {timer.MillisecondsElapsedThisTurn} " + // #DEBUG
								  $"nodes {nodes} " + // #DEBUG
								  $"nps {nodes * 1000 / elapsed} " + // #DEBUG
								  $"pv {rootBestMove.ToString().Substring(7, rootBestMove.ToString().Length - 8)}"); // #DEBUG
			} // #DEBUG
		}
		catch { }
		return rootBestMove;
	}
}