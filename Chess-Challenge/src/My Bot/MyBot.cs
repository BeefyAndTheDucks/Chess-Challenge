using ChessChallenge.API;
using System.Collections.Generic;
using static System.Math;
using static ChessChallenge.API.PieceType;

public class MyBot : IChessBot
{
    // This bot is somehow better when it is black???

    private Board gameBoard;
    private Move bestMove;

    public Move Think(Board board, Timer timer)
    {
        gameBoard = board;
        Search((timer.MillisecondsRemaining <= 40000) ? 4 : 5, 0, -9999999, 9999999); // If the time left is below 40.001 seconds, then activate "PANIC MODE" (just reducing the depth to 4)

        return bestMove;
    }

    private int Search(int depth, int plyFromRoot, int alpha, int beta)
    {
        if (plyFromRoot > 0)
        {
            if (gameBoard.IsDraw())
                return 0;

            alpha = Max(alpha, -100000 + plyFromRoot);
            beta = Min(beta, 100000 - plyFromRoot);
            if (alpha >= beta)
                return alpha;
        }

        if (depth == 0)
            return SearchAllCaptures(alpha, beta);

        var moves = GetMoves(false);
        OrderMoves(ref moves);

        // Detect checkmate and stalemate when no legal moves are available
        if (moves.Count == 0)
            return gameBoard.IsInCheck() ? -100000 + plyFromRoot : 0;

        foreach (var move in moves)
        {
            gameBoard.MakeMove(move);
            int eval = -Search(depth - 1, plyFromRoot + 1, -beta, -alpha);
            gameBoard.UndoMove(move);

            // Move was *too* good, so opponent won't allow this position to be reached
            // (by choosing a different move earlier on). Skip remaining moves.
            if (eval >= beta)
                return beta;

            // Found a new best move in this position
            if (eval > alpha)
            {
                alpha = eval;
                if (plyFromRoot == 0)
                    bestMove = move;
            }
        }

        return alpha;
    }

    private int SearchAllCaptures(int alpha, int beta)
    {
        int eval = Evaluate();

        if (eval >= beta)
            return beta;

        if (eval > alpha)
            alpha = eval;

        var moves = GetMoves(true);
        OrderMoves(ref moves);
        foreach (var move in moves)
        {
            gameBoard.MakeMove(move);
            eval = -SearchAllCaptures(-beta, -alpha);
            gameBoard.UndoMove(move);

            if (eval >= beta)
                return beta;

            if (eval > alpha)
                alpha = eval;
        }

        return alpha;
    }

    private List<Move> GetMoves(bool onlyCaptures) => new List<Move>(gameBoard.GetLegalMoves(onlyCaptures));

    private int GetPieceValue(PieceType pieceType) =>
        pieceType switch
        {
            Pawn => 100,
            Knight or Bishop => 300,
            Rook => 500,
            Queen => 900,
            _ => 0,
        };

    private int GetPieceCount(PieceType type, bool white) => gameBoard.GetPieceList(type, white).Count;

    private void OrderMoves(ref List<Move> moves)
    {
        var scores = new Dictionary<Move, int>();

        foreach (var move in moves)
        {
            var scoreGuess = 0;

            if (move.CapturePieceType != None)
                scoreGuess = 10 * GetPieceValue(move.MovePieceType) - GetPieceValue(move.CapturePieceType);

            if (move.IsPromotion)
                scoreGuess += GetPieceValue(move.PromotionPieceType);

            // If I get more tokens, I should add this:
            // -- [UNTESTED] -- \\

            // -- OPTIMISATION START -- \\

            //gameBoard.MakeMove(move);
            //scoreGuess += Evaluate(); // Unsure if this should be '+=' or '-=' -_-
            //gameBoard.UndoMove(move);

            // -- OPTIMISATION END -- \\

            scores[move] = scoreGuess;
        }

        moves.Sort((Move x, Move y) => scores[y].CompareTo(scores[x]));
    }

    private int Evaluate()
    {
        int whiteMaterial = (GetPieceCount(Pawn, true) * 100) +
                            ((GetPieceCount(Knight, true) +
                            GetPieceCount(Bishop, true)) * 300) +
                            (GetPieceCount(Rook, true) * 500) +
                            (GetPieceCount(Queen, true) * 900);

        return (EvaluatePieceSquareTables(true, EndgamePhaseWeight(-whiteMaterial - GetPieceCount(Pawn, false) * 100)) + whiteMaterial - (EvaluatePieceSquareTables(false, EndgamePhaseWeight(whiteMaterial - GetPieceCount(Pawn, true) * 100)) + -whiteMaterial)) * (gameBoard.IsWhiteToMove ? 1 : -1);
    }

    private float EndgamePhaseWeight(int materialCountWithoutPawns) => 1 - Min(1, materialCountWithoutPawns * 0.0003f);

    private int EvaluatePieceSquareTables(bool isWhite, float endgamePhaseWeight) =>
                EvaluatePieceSquareTable(new int[] {
        70, 70, 70, 70,
        50, 50, 50, 50,
        10, 10, 20, 30,
        5,  5, 10, 25,
        0,  0,  0, 20,
        5, -5, -10, 0,
        5, 10, 10,-20,
        0,  0,  0,  0
    }, gameBoard.GetPieceList(Pawn, isWhite), isWhite) +
                EvaluatePieceSquareTable(new int[]{
        -50,-40,-30,-30,
        -40,-20,  0,  0,
        -30,  0, 10, 15,
        -30,  5, 15, 20,
        -30,  0, 15, 20,
        -30,  5, 10, 15,
        -40,-20,  0,  5,
        -50,-40,-30,-30,
                }, gameBoard.GetPieceList(Knight, isWhite), isWhite) +
                EvaluatePieceSquareTable(new int[]{
        -20,-10,-10,-10,
        -10,  0,  0,  0,
        -10,  0,  5, 10,
        -10,  5,  5, 10,
        -10,  0, 10, 10,
        -10, 10, 10, 10,
        -10,  5,  0,  0,
        -20,-10,-10,-10,
    }, gameBoard.GetPieceList(Bishop, isWhite), isWhite) +
                EvaluatePieceSquareTable(new int[]{
         0,  0,  0,  0,
         5, 10, 10, 10,
        -5,  0,  0,  0,
        -5,  0,  0,  0,
        -5,  0,  0,  0,
        -5,  0,  0,  0,
        -5,  0,  0,  0,
         0,  0,  0,  5,
    }, gameBoard.GetPieceList(Rook, isWhite), isWhite) +
                EvaluatePieceSquareTable(new int[]{
        -20,-10,-10, -5,
        -10,  0,  0,  0,
        -10,  0,  5,  5,
        -5,   0,  5,  5,
         0,   0,  5,  5,
        -10,  5,  5,  5,
        -10,  0,  5,  0,
        -20,-10,-10, -5,
    }, gameBoard.GetPieceList(Queen, isWhite), isWhite) +
                (int)(Read(new int[] {
        -30,-40,-40,-50,
        -30,-40,-40,-50,
        -30,-40,-40,-50,
        -30,-40,-40,-50,
        -20,-30,-30,-40,
        -10,-20,-20,-20,
         20, 20,  0,  0,
         20, 30, 10,  0,
    }, gameBoard.GetKingSquare(isWhite), isWhite) * (1 - endgamePhaseWeight));

    // Had to shave tokens... Ended up with this mess

    private int EvaluatePieceSquareTable(int[] table, PieceList pieceList, bool isWhite)
    {
        int value = 0;
        for (int i = 0; i < pieceList.Count; i++)
            value += Read(table, pieceList[i].Square, isWhite);
        return value;
    }

    private int Read(int[] table, Square sqr, bool isWhite)
    {
        int square = isWhite ? (7 - sqr.Rank) * 8 + sqr.File : sqr.Index;

        var squareIndexMod8 = square % 8;

        square -= 4 * (square / 8);

        if (squareIndexMod8 > 3)
            square -= (squareIndexMod8 - 4) * 2 + 1;

        return table[square];
    }
}