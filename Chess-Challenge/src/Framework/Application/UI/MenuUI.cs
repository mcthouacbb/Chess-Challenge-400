﻿using Raylib_cs;
using System.Numerics;
using System;
using System.IO;

namespace ChessChallenge.Application
{
    public static class MenuUI
    {
        public static void DrawButtons(ChallengeController controller)
        {
            Vector2 buttonPos = UIHelper.Scale(new Vector2(150, 210));
            Vector2 buttonSize = UIHelper.Scale(new Vector2(230, 55));
            float spacing = buttonSize.Y * 1.2f;
            float breakSpacing = spacing * 0.6f;

            // Page buttons
            buttonPos.Y += breakSpacing;

            if (NextButtonInRow("Save Games", ref buttonPos, spacing, buttonSize))
            {
                string pgns = controller.AllPGNs;
                string directoryPath = Path.Combine(FileHelper.AppDataPath, "Games");
                Directory.CreateDirectory(directoryPath);
                string fileName = FileHelper.GetUniqueFileName(directoryPath, "games", ".txt");
                string fullPath = Path.Combine(directoryPath, fileName);
                File.WriteAllText(fullPath, pgns);
                ConsoleHelper.Log("Saved games to " + fullPath, false, ConsoleColor.Blue);
            }
            if (NextButtonInRow("Rules & Help", ref buttonPos, spacing, buttonSize))
            {
                FileHelper.OpenUrl("https://github.com/SebLague/Chess-Challenge");
            }
            if (NextButtonInRow("Documentation", ref buttonPos, spacing, buttonSize))
            {
                FileHelper.OpenUrl("https://seblague.github.io/chess-coding-challenge/documentation/");
            }
            if (NextButtonInRow("Submission Page", ref buttonPos, spacing, buttonSize))
            {
                FileHelper.OpenUrl("https://forms.gle/6jjj8jxNQ5Ln53ie6");
			}

			// Window and quit buttons
			buttonPos.Y += breakSpacing;

            bool isBigWindow = Raylib.GetScreenWidth() > Settings.ScreenSizeSmall.X;
            string windowButtonName = isBigWindow ? "Smaller Window" : "Bigger Window";
            if (NextButtonInRow(windowButtonName, ref buttonPos, spacing, buttonSize))
            {
                Program.SetWindowSize(isBigWindow ? Settings.ScreenSizeSmall : Settings.ScreenSizeBig);
            }
            if (NextButtonInRow("Exit (ESC)", ref buttonPos, spacing, buttonSize))
            {
                Environment.Exit(0);
            }

			buttonPos = UIHelper.Scale(new Vector2(400, 210));

			// Game Buttons
			if (NextButtonInRow("Human vs MyBot", ref buttonPos, spacing, buttonSize))
			{
				var whiteType = controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.MyBot : ChallengeController.PlayerType.Human;
				var blackType = !controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.MyBot : ChallengeController.PlayerType.Human;
				controller.StartNewGame(whiteType, blackType);
			}
            if (NextButtonInRow("Human vs AtadOfANoobBot", ref buttonPos, spacing, buttonSize))
			{
				var whiteType = controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.AtadOfANoobBot : ChallengeController.PlayerType.Human;
				var blackType = !controller.HumanWasWhiteLastGame ? ChallengeController.PlayerType.AtadOfANoobBot : ChallengeController.PlayerType.Human;
				controller.StartNewGame(whiteType, blackType);
			}
			if (NextButtonInRow("AtadOfANoobBot vs P4kBotOld", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.AtadOfANoobBot, ChallengeController.PlayerType.P4kBotOld);
			}
			if (NextButtonInRow("NOSPRTBot vs AtadOfANoobBot", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.NOSPRTBot, ChallengeController.PlayerType.AtadOfANoobBot);
			}
			if (NextButtonInRow("EvilBot vs AtadOfANoobBot", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.EvilBot, ChallengeController.PlayerType.AtadOfANoobBot);
			}
			if (NextButtonInRow("MyBot vs MyBot", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.MyBot);
			}
			if (NextButtonInRow("MyBot vs EvilBot", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.EvilBot);
			}
			if (NextButtonInRow("MyBot vs MyBotOld", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.MyBotOld);
			}
			if (NextButtonInRow("MyBot vs Stockfish", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.Stockfish);
			}
			if (NextButtonInRow("MyBot vs Negamax", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.NegamaxBot);
			}
			if (NextButtonInRow("My Bot vs Negamax2Bot", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.Negamax2Bot);
			}
			if (NextButtonInRow("My Bot vs AugsEUBot", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.AugsEUBot);
			}
			if (NextButtonInRow("My Bot vs CmndrBot", ref buttonPos, spacing, buttonSize))
			{
				controller.StartNewBotMatch(ChallengeController.PlayerType.MyBot, ChallengeController.PlayerType.CmndrBot);
			}

			bool NextButtonInRow(string name, ref Vector2 pos, float spacingY, Vector2 size)
            {
                bool pressed = UIHelper.Button(name, pos, size);
                pos.Y += spacingY;
                return pressed;
            }
        }
    }
}