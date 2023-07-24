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
            RegularSearch(5, 0, negativeInfinity, positiveInfinity, false, timer);

        if (timer.MillisecondsRemaining <= abortSearchTimeLeftMillis)
            RegularSearch(4, 0, negativeInfinity, positiveInfinity, InsufficentMaterial(board.IsWhiteToMove) || timer.MillisecondsRemaining < lookForDrawTimeLeftMillis, timer);
        return moveToPlay;
    }

    public MyBot()
    {
        // Init some settings
        immediateMateScore = 100000;
        positiveInfinity = 9999999;
        negativeInfinity = -positiveInfinity;
        drawScore = 10000000;
        abortSearchTimeLeftMillis = 20000;
        lookForDrawTimeLeftMillis = 10000;
    }

    private int Evaluate(bool white)
    {
        var eval = CountMaterial(true) - CountMaterial(false);

        return eval * (white ? 1 : -1);
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

    private int RegularSearch(int depth, int plyFromRoot, int alpha, int beta, bool findDraw, Timer timer)
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
            int eval = -RegularSearch(depth - 1, plyFromRoot + 1, -beta, -alpha, findDraw, timer);
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

    // Search capture moves until a 'quiet' position is reached.
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
}

// -- CHAT AREA -- \\
/*
We can also chat like this (with comments)
There is a chat on the right side
alright let me get the code
looks nice
You can paste your Alpha-beta code
hold on let me get my mouse    sure
code looks nice
alright this is the draw bot right?
yeah - also nice bot name xD
We could submit it under "DrawBot"
yeah that's a good name
sebastion is accepting intersting bots
yeah - he might feature it
so, how do we incourage a draw
I was thinking Repetition or Fiftymoverule
well first we gotta make the eval max when a draw happens
Ill make the impossible scenario detection
what's that/?
the code that detects if there is no way the bot can win
can't we do that with a simple search
yeah, but that might use a lot of tokens...
true, mabye we can enforce trades
just so it's harder for both bots to mate, yeah excatly
alright, do we work on eval? Sure
thats the best way we can go about it
since it wont take up that much time
let me paste my eval and make tweaks
alright, I will detect insuff. material then
Also - we could give squares bonusses based on each piece - like in the first chess episode
we could also check if there is "insufficient material"
*/

//Code
/*
//This code was from "Algorithms Explained – minimax and alpha-beta pruning" by Sebastian Lague
    public int MinMax(Board board, Timer timer, int depth, int alpha, int beta, bool maximizingPlayer)
    {
        //int evaluation = 0;

        Move[] moves = board.GetLegalMoves();
        if (depth == 0 || board.IsInCheckmate() || board.IsDraw())
        {
            return Evaluate(board, timer);
        }
        if (maximizingPlayer)
        {
            int maxEval = -2147483647;
            foreach (Move currentMove in moves)
            {

                board.MakeMove(currentMove);
                
                
                int evaluation = MinMax(board, timer, depth - 1, alpha, beta, false);
                board.UndoMove(currentMove);
                maxEval = Math.Max(maxEval, evaluation);
                alpha = Math.Max(alpha, evaluation);
                if (beta <= alpha) 
                {
                   break;
                }
            }
            
            return maxEval;
        }
        else 
        {
            int minEval = 2147483647;
            foreach (Move currentMove in moves)
            {
                board.MakeMove(currentMove);
                int evaluation = MinMax(board, timer, depth - 1, alpha, beta, true);
                board.UndoMove(currentMove);
                minEval = Math.Min(minEval, evaluation);
                beta = Math.Min(beta, evaluation);
                if (beta <= alpha)
                {
                   break;
                }
            }
            
            return minEval;
        }
        return 0;
    }
*/