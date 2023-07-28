using ChessChallenge.API;
using ChessChallenge.Application;
using ChessChallenge.Application.APIHelpers;
using ChessChallenge.Chess;
using System;

/* UCI COMMAND: cutechess -engine name="MyBot" cmd="Chess-Challenge.exe uci MyBot" -engine name="MyBotOld" cmd="Chess-Challenge.exe uci MyBotOld" -each proto=uci tc=10+0.1 -concurrency 4 -maxmoves 200 -games 2 -rounds 500 -ratinginterval 20 -openings file="C:\Dev\c++\Sirius\Sirius\res\2moves_v2.pgn" format=pgn order=random -pgnout error"C:\Users\Aidanmicro.com\AppData\Local\ChessCodingChallenge\Games\MyBot-vs-MyBotOld-cc-1.pgn" -srand %RANDOM%
 * ignore this if you're not me lol
 */

namespace ChessChallenge.UCI
{
    class UCIBot
    {
        IChessBot bot;
        ChallengeController.PlayerType type;
        Chess.Board board;
        APIMoveGen moveGen;

        static readonly string defaultFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

		public UCIBot(IChessBot bot, ChallengeController.PlayerType type)
        {
            this.bot = bot;
            this.type = type;
            moveGen = new APIMoveGen();
            board = new Chess.Board();
        }

        void PositionCommand(string[] args)
        {
            int idx = Array.FindIndex(args, x => x == "moves");
            if (idx == -1)
            {
                if (args[1] == "startpos")
                {
                    board.LoadStartPosition();
                }
                else
                {
                    board.LoadPosition(String.Join(" ", args.AsSpan(1, args.Length - 1).ToArray()));
                }
            }
            else
            {
                if (args[1] == "startpos")
				{
					board.LoadStartPosition();
				}
                else
				{
					board.LoadPosition(String.Join(" ", args.AsSpan(1, idx - 1).ToArray()));
				}

                for (int i = idx + 1; i < args.Length; i++)
                {
                    // this is such a hack
                    API.Move move = new API.Move(args[i], new API.Board(board));
                    board.MakeMove(new Chess.Move(move.RawValue), false);
                }
            }

            string fen = FenUtility.CurrentFen(board);
            Console.WriteLine(fen);
        }

        void GoCommand(string[] args)
        {
            int wtime = 0, btime = 0;
            API.Board apiBoard = new API.Board(board);
            Console.WriteLine(FenUtility.CurrentFen(board));
            Console.WriteLine(apiBoard.GetFenString());
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "wtime")
                {
                    wtime = Int32.Parse(args[i + 1]);
                }
                else if (args[i] == "btime")
                {
                    btime = Int32.Parse(args[i + 1]);
                }
            }
            if (!apiBoard.IsWhiteToMove)
            {
                int tmp = wtime;
                wtime = btime;
                btime = tmp;
            }
            Timer timer = new Timer(wtime, btime, 0);
            API.Move move = bot.Think(apiBoard, timer);
            Console.WriteLine($"bestmove {move.ToString().Substring(7, move.ToString().Length - 8)}");
        }

        void ExecCommand(string line)
        {
            // default split by whitespace
            var tokens = line.Split();

            if (tokens.Length == 0)
                return;

            switch (tokens[0])
            {
                case "uci":
                    Console.WriteLine("id name Chess Challenge");
                    Console.WriteLine("id author AspectOfTheNoob, Sebastian Lague");
                    Console.WriteLine("uciok");
                    break;
                case "ucinewgame":
                    bot = ChallengeController.CreateBot(type);
                    break;
                case "position":
                    PositionCommand(tokens);
                    break;
                case "isready":
                    Console.WriteLine("readyok");
                    break;
                case "go":
                    GoCommand(tokens);
                    break;
            }
        }

        public void Run()
        {
            while (true)
            {
                string line = Console.ReadLine();

                if (line == "quit" || line == "exit")
                    return;
                ExecCommand(line);
            }
        }
    }
}
