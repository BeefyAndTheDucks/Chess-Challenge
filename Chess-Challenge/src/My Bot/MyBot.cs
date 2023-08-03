using ChessChallenge.API;
using System;
using static ChessChallenge.API.PieceType;
using static System.Math;

public class MyBot : IChessBot
{
    private Board board; // Cached version of the board
    private Timer timer;
    private Move bestMoveThisIteration;
    private int bestEvalThisIteration;
    private bool botIsWhite;

    private readonly int immediateMateScore = 100000;
    private readonly int positiveInfinity = 9999999;
    private readonly int negativeInfinity = -9999999;
    private readonly int drawScore = 90000;

    private int currentSearchDepth;
    private bool abortSearch => timer.MillisecondsElapsedThisTurn >= 1000 && currentSearchDepth > 1; // Search Time.

    public Move Think(Board gameBoard, Timer botTimer)
    {
        botIsWhite = gameBoard.IsWhiteToMove;

        board = gameBoard; // Cache board
        timer = botTimer;

        currentSearchDepth = 0;
        while (!(abortSearch || IsMateScore(bestEvalThisIteration)))
        {
            System.Diagnostics.Stopwatch stopwatch = new(); // #DEBUG
            stopwatch.Start(); // #DEBUG
            Search(++currentSearchDepth, 0, negativeInfinity, positiveInfinity, InsufficentMaterial());
            stopwatch.Stop(); // #DEBUG
            Console.WriteLine($"[DrawBot] Depth {currentSearchDepth} took {stopwatch.ElapsedMilliseconds} ms."); // #DEBUG
        }

        Console.WriteLine($"[DrawBot] Depth {currentSearchDepth - 1}"); // #DEBUG

        return bestMoveThisIteration;
    }
    private int CountMaterial(bool white) =>
        GetPieceCount(Pawn, white) * 100 +
        GetPieceCount(Knight, white) * 300 +
        GetPieceCount(Bishop, white) * 300 +
        GetPieceCount(Rook, white) * 500 +
        GetPieceCount(Queen, white) * 900;


    private int GetPieceCount(PieceType type, bool white) => board.GetPieceList(type, white).Count;

    public bool IsMateScore(int score) => Abs(score) > immediateMateScore - 1000;

    public bool InsufficentMaterial()
    {
        if (GetPieceCount(Pawn, botIsWhite) > 0 || GetPieceCount(Queen, botIsWhite) + GetPieceCount(Rook, botIsWhite) > 0)
            return false;

        if (GetPieceCount(Bishop, botIsWhite) + GetPieceCount(Knight, botIsWhite) <= 1)
            return true;

        return false;
    }
    private int Search(int depth, int plyFromRoot, int alpha, int beta, bool findDraw)
    {
        if (abortSearch)
            return 0;

        if (board.IsDraw())
            if (findDraw && (!board.IsWhiteToMove) == botIsWhite)
                if (!board.IsWhiteToMove == botIsWhite)
                    return drawScore;
                else
                    return 0;


        if (plyFromRoot > 0)
        {
            alpha = Max(alpha, -10000 + plyFromRoot);
            beta = Min(beta, immediateMateScore - plyFromRoot);
            if (alpha >= beta)
                return alpha;
        }

        if (depth == 0)
            return QuiescenceSearch(alpha, beta);

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves);
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
            int eval = -Search(depth - 1, plyFromRoot + 1, -beta, -alpha, findDraw);
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
                    bestMoveThisIteration = move;
                    bestEvalThisIteration = eval;
                }
            }
        }

        return alpha;
    }

    /// Search capture moves until a 'quiet' position is reached.
    private int QuiescenceSearch(int alpha, int beta)
    {
        int eval = Evaluate();
        if (eval >= beta)
            return beta;

        if (eval > alpha)
            alpha = eval;

        Span<Move> moves = stackalloc Move[256];
        board.GetLegalMovesNonAlloc(ref moves, true);
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

    private void OrderMoves(ref Span<Move> moves)
    {
        var _moves = moves.ToArray();
        Array.Sort(_moves, (moveA, moveB) => GetScoreGuess(moveA).CompareTo(GetScoreGuess(moveB)));
        moves = _moves;
    }

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

    private int Evaluate()
    {
        var whiteMaterial = CountMaterial(true);
        var blackMaterial = CountMaterial(false);
        return
            (whiteMaterial + EvaluatePieceSquareTables(true, EndgamePhaseWeight(whiteMaterial - GetPieceCount(Pawn, true) * 100)) -
            (blackMaterial + EvaluatePieceSquareTables(false, EndgamePhaseWeight(blackMaterial - GetPieceCount(Pawn, false) * 100)))) * (board.IsWhiteToMove ? 1 : -1);
    }

    private float EndgamePhaseWeight(int materialCountWithoutPawns) => 1 - Min(1, materialCountWithoutPawns * 0.0003f);

    private int EvaluatePieceSquareTables(bool white, float endgamePhaseWeight)
    {
        var kingSquare = board.GetKingSquare(white);

        return EvaluatePieceSquareTable(ScoreType.Pawn, board.GetPieceList(Pawn, white), white) +
            EvaluatePieceSquareTable(ScoreType.Knight, board.GetPieceList(Knight, white), white) +
            EvaluatePieceSquareTable(ScoreType.Bishop, board.GetPieceList(Bishop, white), white) +
            EvaluatePieceSquareTable(ScoreType.Rook, board.GetPieceList(Rook, white), white) +
            EvaluatePieceSquareTable(ScoreType.Queen, board.GetPieceList(Queen, white), white) +
            GetPieceBonusScore((endgamePhaseWeight >= 0.5f) ? (endgamePhaseWeight >= 0.8f) ? ScoreType.KingHunt : ScoreType.KingEndgame : ScoreType.King, white, kingSquare.Rank, kingSquare.File);
    }

    private int EvaluatePieceSquareTable(ScoreType type, PieceList pieceList, bool white)
    {
        var value = 0;
        foreach (Piece piece in pieceList)
            value += GetPieceBonusScore(type, white, piece.Square.Rank, piece.Square.File);

        return value;
    }

    //enumeration to keep track externally of 
    //which byte is for which scores
    private enum ScoreType { Pawn, Knight, Bishop, Rook, Queen, King, KingEndgame, KingHunt };

    //Assuming you put your packed data table into a table called packedScores.
    private int GetPieceBonusScore(ScoreType type, bool isWhite, int rank, int file)
    {
        //Because the arrays are 8x4, we need to mirror across the files.
        if (file > 3) file = 7 - file;
        //Additionally, if we're checking black pieces, we need to flip the board vertically.
        if (!isWhite) rank = 7 - rank;
        ulong bytemask = 0xFF;
        //first we shift the mask to select the correct byte              ↓
        //We then bitwise-and it with PackedScores            ↓
        //We finally have to "un-shift" the resulting data to properly convert back       ↓
        //We convert the result to an sbyte, then to an int, to ensure it converts properly.
        var unpackedData = (int)(sbyte)((packedScores[rank, file] & (bytemask << (int)type)) >> (int)type);
        //inverting eval scores for black pieces
        if (!isWhite) unpackedData *= -1;
        return unpackedData;
    }

    private readonly ulong[,] packedScores =
    {
        {0x31CDE1EBFFEBCE00, 0x31D7D7F5FFF5D800, 0x31E1D7F5FFF5E200, 0x31EBCDFAFFF5E200},
        {0x31E1E1F604F5D80A, 0x13EBD80009FFEC0A, 0x13F5D8000A000014, 0x13FFCE000A00001E},
        {0x31E1E1F5FAF5E232, 0x13F5D80000000032, 0x0013D80500050A32, 0x001DCE05000A0F32},
        {0x31E1E1FAFAF5E205, 0x13F5D80000050505, 0x001DD80500050F0A, 0xEC27CE05000A1419},
        {0x31E1EBFFFAF5E200, 0x13F5E20000000000, 0x001DE205000A0F00, 0xEC27D805000A1414},
        {0x31E1F5F5FAF5E205, 0x13F5EC05000A04FB, 0x0013EC05000A09F6, 0x001DEC05000A0F00},
        {0x31E213F5FAF5D805, 0x13E214000004EC0A, 0x140000050000000A, 0x14000000000004EC},
        {0x31CE13EBFFEBCE00, 0x31E21DF5FFF5D800, 0x31E209F5FFF5E200, 0x31E1FFFB04F5E200},
    };
}

/*
int MinMax (Board board, Timer timer, int, depth, int alpha, int beta, bool maximazingPlayer)
int bestEval = maximizingPlayer ? -2147483647 : 2147483647;
foreach (Move currentMove in moves)
{

    board.MakeMove(currentMove);

    int evaluation = MinMax(board, timer, depth - 1, alpha, beta, !maximizingPlayer);
    board.UndoMove(currentMove);
    bestEval = maximizingPlayer ? Math.Max(bestEval, evaluation) : Math.Min(bestEval, evaluation);
    alpha = maximizingPlayer ? Math.Max(alpha, evaluation) : alpha;
    beta = maximizingPlayer ? beta : Math.Min(beta, evaluation);
    if (beta <= alpha)
    {
        break;
    }
}

return bestEval;
} // this is almost the same code as our current one
// our current one is just much more optimize


*/