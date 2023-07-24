using ChessChallenge.API;
using System;
using static ChessChallenge.API.PieceType;
using static System.Math;
// also it says i'm following your cursor, is there a way to stop that I don't know...
public class MyBot : IChessBot
{
    private Board board; // Cached version of the board
    private Move moveToPlay;
    private bool botIsWhite;

    private readonly int immediateMateScore;
    private readonly int positiveInfinity;
    private readonly int negativeInfinity;
    private readonly int drawScore;
    private readonly int abortSearchTimeLeftMillis;
    private readonly int lookForDrawTimeLeftMillis;

    private int searchStartMillisRemaining;

    public Move Think(Board gameBoard, Timer timer)
    {
        botIsWhite = gameBoard.IsWhiteToMove;

        board = gameBoard; // Cache board

        if (timer.MillisecondsRemaining > abortSearchTimeLeftMillis)
            Search(5, 0, negativeInfinity, positiveInfinity, false, timer);

        if (timer.MillisecondsRemaining <= abortSearchTimeLeftMillis)
            Search(4, 0, negativeInfinity, positiveInfinity, InsufficentMaterial(board.IsWhiteToMove) || timer.MillisecondsRemaining < lookForDrawTimeLeftMillis, timer);
        return moveToPlay;
    }

    public MyBot()
    {
        // -- SETTINGS -- \\
        immediateMateScore = 100000;
        positiveInfinity = 9999999;
        negativeInfinity = -positiveInfinity;
        drawScore = 10000000;
        abortSearchTimeLeftMillis = 20000;
        lookForDrawTimeLeftMillis = 10000;
    }
    
    

    private int CountMaterial(bool white) =>
        GetPieceCount(Pawn, white) * 100 +
        GetPieceCount(Knight, white) * 300 +
        GetPieceCount(Bishop, white) * 300 +
        GetPieceCount(Rook, white) * 500 +
        GetPieceCount(Queen, white) * 900;


    private int GetPieceCount(PieceType type, bool white) => board.GetPieceList(type, white).Count;

    // Test for insufficient material (Note: not all cases are implemented)
    public bool InsufficentMaterial(bool friendlyIsWhite)
    {
        int numFriendlyMinors = GetPieceCount(Bishop, friendlyIsWhite) + GetPieceCount(Knight, friendlyIsWhite);

        // Lone kings or King vs King + single minor: is insuffient
        if (numFriendlyMinors <= 1)
            return true;

        return false;
    }

    private int Search(int depth, int plyFromRoot, int alpha, int beta, bool findDraw, Timer timer)
    {
        if (plyFromRoot == 0)
            searchStartMillisRemaining = timer.MillisecondsRemaining;

        if (timer.MillisecondsRemaining <= abortSearchTimeLeftMillis && !findDraw && searchStartMillisRemaining > abortSearchTimeLeftMillis)
            return 0; // Abort search if the time is running low

        if (board.IsDraw())
        {
            if (findDraw && (!board.IsWhiteToMove) == botIsWhite)
                if (!board.IsWhiteToMove == botIsWhite)
                    return drawScore;
                else
                    return 0;
        }


        if (plyFromRoot > 0)
        {
            alpha = Max(alpha, -10000 + plyFromRoot);
            beta = Min(beta, immediateMateScore - plyFromRoot);
            if (alpha >= beta)
                return alpha;
        }

        if (depth == 0)
            return QuiescenceSearch(alpha, beta);

        var moves = board.GetLegalMoves();
        OrderMoves(ref moves);

        // Detect checkmate and stalemate when no legal moves are available
        if (moves.Length == 0)
            if (board.IsInCheck())
                return -immediateMateScore - plyFromRoot;
            else
                return 0;

        foreach (var move in moves)
        {
            board.MakeMove(move);
            int eval = -Search(depth - 1, plyFromRoot + 1, -beta, -alpha, findDraw, timer);
            board.UndoMove(move);

            // Move was *too* good, so opponent won't allow this position to be reached
            // (by choosing a different move earlier on). Skip remaining moves.
            if (eval >= beta)
                return beta;

            // Found a new best move in this position
            if (eval > alpha)
            {
                alpha = eval;
                if (plyFromRoot == 0)
                {
                    moveToPlay = move;
                }
            }
        }

        return alpha;
    }

    /// Search capture moves until a 'quiet' position is reached.
    private int QuiescenceSearch(int alpha, int beta)
    {
        int eval = Evaluate(board.IsWhiteToMove);
        if (eval >= beta)
            return beta;

        if (eval > alpha)
            alpha = eval;

        var moves = board.GetLegalMoves(true);
        OrderMoves(ref moves);

        foreach (var move in moves)
        {
            board.MakeMove(move);
            eval = -QuiescenceSearch(-beta, -alpha);
            board.UndoMove(move);

            if (eval >= beta)
                return beta;

            if (eval > alpha)
                alpha = eval;
        }

        return alpha;
    }

    private int GetPieceValue(PieceType type) => type switch
    {
        Pawn => 100,
        Knight or Bishop => 300,
        Rook => 500,
        Queen => 900,
        _ => 0
    };

    private void OrderMoves(ref Move[] moves) => Array.Sort(moves, (moveA, moveB) => GetScoreGuess(moveA).CompareTo(GetScoreGuess(moveB)));

    private int GetScoreGuess(Move move)
    {
        var score = 0;

        if (move.CapturePieceType != None)
            score = 10 * (GetPieceValue(move.MovePieceType) - GetPieceValue(move.CapturePieceType));

        if (move.IsPromotion)
            score += 10 * GetPieceValue(move.PromotionPieceType);

        if (board.SquareIsAttackedByOpponent(move.TargetSquare))
            score -= 5 * GetPieceValue(move.MovePieceType);

        return score;
    }

    private int Evaluate(bool white)
    {
        var whiteMaterial = CountMaterial(true);
        var blackMaterial = CountMaterial(false);

        var whiteMaterialWithoutPawns = whiteMaterial - GetPieceCount(Pawn, true) * 100;
        var blackMaterialWithoutPawns = blackMaterial - GetPieceCount(Pawn, false) * 100;

        var whiteEval = whiteMaterial + EvaluatePieceSquareTables(true, EndgamePhaseWeight(whiteMaterialWithoutPawns));
        var blackEval = blackMaterial + EvaluatePieceSquareTables(false, EndgamePhaseWeight(blackMaterialWithoutPawns)); // OPTIMIZE TOKENS HERE

        var eval = whiteEval - blackEval; 
        return eval * ((board.IsWhiteToMove) ? 1 : -1);
    }

    private float EndgamePhaseWeight(int materialCountWithoutPawns) => 1 - Min(1, materialCountWithoutPawns * 0.0003f);

    private int EvaluatePieceSquareTables(bool white, float endgamePhaseWeight) => 
            EvaluatePieceSquareTable(pawnsBonusSqaures, board.GetPieceList(Pawn, white), white) + 
            EvaluatePieceSquareTable(knightsBonusSqaures, board.GetPieceList(Knight, white), white) +
            EvaluatePieceSquareTable(bishopsBonusSqaures, board.GetPieceList(Bishop, white), white) +
            EvaluatePieceSquareTable(rooksBonusSqaures, board.GetPieceList(Rook, white), white) +
            EvaluatePieceSquareTable(queensBonusSqaures, board.GetPieceList(Queen, white), white) + 
            (int)(ReadPieceSquareTable(kingBonusSqaures, board.GetKingSquare(white), white) * (1 - endgamePhaseWeight));
    
    private int EvaluatePieceSquareTable(int[] table, PieceList pieceList, bool isWhite)
    {
        var value = 0;
        for (int i = 0; i < pieceList.Count; i++)
            value += ReadPieceSquareTable(table, pieceList[i].Square, isWhite);

        return value;
    }

    private int ReadPieceSquareTable(int[] table, Square square, bool isWhite)
    {
        var squareIndex = square.Index;
        
        if (isWhite) // Below equation taken from discord
            squareIndex ^= 0b111000; // Flip the table upside down if we are white (i think lol)

        // Below equation taken from discord
        return table[(square.File > 3 ? square.File ^ 7 : square.File) + (squareIndex / 8) * 4];
    }

    public readonly int[] pawnsBonusSqaures = {
            0,  0,  0,  0, 
            50, 50, 50, 50,
            10, 10, 20, 30,
            5,  5, 10, 25, 
            0,  0,  0, 20, 
            5, -5,-10,  0, 
            5, 10, 10,-20,
            0,  0,  0,  0, 
    };
    
    public readonly int[] knightsBonusSqaures = {
            -50,-40,-30,-30,
            -40,-20,  0,  0,
            -30,  0, 10, 15,
            -30,  5, 15, 20,
            -30,  0, 15, 20,
            -30,  5, 10, 15,
            -40,-20,  0,  5,
            -50,-40,-30,-30,
    };

    public readonly int[] bishopsBonusSqaures = {
            -20,-10,-10,-10,
            -10,  0,  0,  0,
            -10,  0,  5, 10,
            -10,  5,  5, 10,
            -10,  0, 10, 10,
            -10, 10, 10, 10,
            -10,  5,  0,  0,
            -20,-10,-10,-10,
    };

    public readonly int[] rooksBonusSqaures = {
            0,  0,  0,  0, 
            5, 10, 10, 10, 
            -5,  0,  0,  0,
            -5,  0,  0,  0,
            -5,  0,  0,  0,
            -5,  0,  0,  0,
            -5,  0,  0,  0,
            0,  0,  0,  5, 
    };
    
    public static readonly int[] queensBonusSqaures = {
            -20,-10,-10, -5, 
            -10,  0,  0,  0, 
            -10,  0,  5,  5, 
            -5,   0,  5,  5,  
             0,   0,  5,  5,
            -10,  5,  5,  5, 
            -10,  0,  5,  0, 
            -20,-10,-10, -5, 
    };

    public readonly int[] kingBonusSqaures = {
            -30,-40,-40,-50,
            -30,-40,-40,-50,
            -30,-40,-40,-50,
            -30,-40,-40,-50,
            -20,-30,-30,-40,
            -10,-20,-20,-20,
            20,  20,  0,  0,
            20,  30, 10,  0,
    };
}