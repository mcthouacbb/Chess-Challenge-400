using System;
using System.Diagnostics;
using System.IO;
using ChessChallenge.API;
using ChessChallenge.Application;

/*
 * level 0 elo ~1346.82
 * level 1 elo ~1444.2
 * level 2 elo ~1566.11
 * level 3 elo ~1728.97
 * level 4 elo ~1953.27
 * level 5 elo ~2196.78
 * level 6 elo ~2382.58
 * level 7 elo ~2518.23
 * level 8 elo ~2642.01
 * level 9 elo ~2711.14
 * level 10 elo ~2785.65
 * level 11 elo ~2851.08
 * level 12 elo ~2909.65
 * level 13 elo ~2962.84
 * level 14 elo ~3011.69
 * level 15 elo ~3056.69
 * level 16 elo ~3099.22
 * level 17 elo ~3138.91
 * level 18 elo ~3176.37
 * level 19 elo ~3211.89
 * level 20 elo max strength
 */

public class Stockfish : IChessBot
{
	private Process stockfishProcess;
	private StreamWriter Ins() => stockfishProcess.StandardInput;
	private StreamReader Outs() => stockfishProcess.StandardOutput;

	/// <summary>
	/// The skill level of stockfish. Max is 20, min is 0.
	/// </summary>

	public Stockfish(int skillLevel)
	{
		var stockfishExe = Environment.GetEnvironmentVariable("STOCKFISH_EXE", EnvironmentVariableTarget.Machine);
		if (stockfishExe == null)
		{
			throw new Exception("Missing environment variable: 'STOCKFISH_EXE'");
		}

		stockfishProcess = new();
		stockfishProcess.StartInfo.RedirectStandardOutput = true;
		stockfishProcess.StartInfo.RedirectStandardInput = true;
		stockfishProcess.StartInfo.FileName = stockfishExe;
		stockfishProcess.Start();

		Ins().WriteLine("uci");
		string? line;
		var isOk = false;

		while ((line = Outs().ReadLine()) != null)
		{
			if (line == "uciok")
			{
				isOk = true;
				break;
			}
		}

		if (!isOk)
		{
			throw new Exception("Failed to communicate with stockfish");
		}

		//Ins().WriteLine($"setoption name Skill Level value {skillLevel}");
	}

	public Move Think(Board board, Timer timer)
	{
		Ins().WriteLine("ucinewgame");
		Ins().WriteLine($"position fen {board.GetFenString()}");
		var timeString = board.IsWhiteToMove ? "wtime" : "btime";
		var oppTimeString = board.IsWhiteToMove ? "btime" : "wtime";
		Ins().WriteLine($"go {timeString} {timer.MillisecondsRemaining} {oppTimeString} {Settings.GameDurationMilliseconds} winc 0 binc 0");

		string? line;
		Move? move = null;

		while ((line = Outs().ReadLine()) != null)
		{
			if (line.StartsWith("bestmove"))
			{
				var moveStr = line.Split()[1];
				move = new Move(moveStr, board);

				break;
			}
		}

		if (move == null)
		{
			throw new Exception("Engine crashed");
		}

		return (Move)move;
	}
}