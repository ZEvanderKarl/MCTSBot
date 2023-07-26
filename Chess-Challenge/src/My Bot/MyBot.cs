using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;
public class MyBot : IChessBot
{
    private Random random;

    public MyBot()
    {
        random = new Random();
    }
    public Move Think(Board board, Timer timer)
    {
        System.Console.WriteLine("Thinking...");
        const int maxIterations = 200;
        double timeThreshold = 1000; // Milliseconds threshold to stop iterations

        
        bool playerIsWhite = board.IsWhiteToMove; // We white?

        // Create the root node representing the current game state

        MonteCarloNode rootNode = new(Move.NullMove, board);

        int iterations = 0;
        while (iterations < maxIterations && timer.MillisecondsElapsedThisTurn < timeThreshold)
        {
            
            // Selection phase
            // System.Console.WriteLine("Entering Selection phase");

            MonteCarloNode nodeToPass = SelectNode(rootNode);
            // System.Console.WriteLine("Selecting node "+nodeToPass.Action);
            
            double result;
            // Move on to Expansion and Simulation only if selected node doesn't end the game.
            if(!nodeToPass.Board.IsInCheckmate() && !nodeToPass.Board.IsDraw()){

                // Expansion phase
                nodeToPass = ExpandNode(nodeToPass);

                // Check AGAIN for end of game
                if(!nodeToPass.Board.IsInCheckmate() && !nodeToPass.Board.IsDraw()) {

                    Move randomMove = GetRandomLegalMove(nodeToPass.Board);
                    Board copiedBoard = CloneBoard(nodeToPass.Board); // Create a copy of the current board state
                    copiedBoard.MakeMove(randomMove); // Apply the random move to the copied board

                    // Simulation phase (playout)
                    // System.Console.WriteLine("Entering Simulation phase");

                    result = SimulateRandomPlayoutWin(copiedBoard, playerIsWhite) ? 1.0 : 0.0; // Result is 1 if WE win, 0 elsewise.
                    
                }else{
                    result = nodeToPass.Board.IsInCheckmate() && !(playerIsWhite^nodeToPass.Board.IsWhiteToMove) ? 1.0 : 0.0;
                }
                
            }else{
                result = nodeToPass.Board.IsInCheckmate() && !(playerIsWhite^nodeToPass.Board.IsWhiteToMove) ? 1.0 : 0.0;
            }
            
            // Backpropagation phase
            // System.Console.WriteLine("Entering Backpropagation phase");
            Backpropagate(nodeToPass, result);

            iterations++;
        }

        // Choose the best action to play based on the visit counts of the root's children
        Move bestAction=Move.NullMove;
        double bestVisitCount = -1;
        System.Console.WriteLine("Children: "+rootNode.Children.Count);
        foreach (MonteCarloNode child in rootNode.Children)
        {
            System.Console.WriteLine(child.Action+": Visit Score="+child.Visits+", UCTValue="+child.UCTValue());
            if (child.Visits > bestVisitCount)
            {
                bestVisitCount = child.Visits;
                bestAction = child.Action;
                
            }
        }
        System.Console.WriteLine(bestAction+" is best! Visited: "+bestVisitCount);
        return bestAction;
    }
    private MonteCarloNode SelectNode(MonteCarloNode node)
    {
        Move[] moves = node.Board.GetLegalMoves();

        // If node's children are populated and positive, node is not leaf and child should be selected recursively according to UCT value.
        // While recursive method most obviously elegant, consider rewriting to not be recursive.
        if(node.Children.Count >0 && node.Children.Count != moves.Length) System.Console.WriteLine("You litte fucker");
        if (node.Children.Count>0)
        {
            // System.Console.WriteLine("Node "+node.Action+" Children:");
            // foreach (MonteCarloNode child in node.Children) System.Console.WriteLine(child.Action+" UCTValue: "+child.UCTValue());
            System.Console.WriteLine(node.Children.OrderByDescending(n => n.UCTValue()).First().UCTValue());
            return SelectNode(node.Children.OrderByDescending(n => n.UCTValue()).First());
        }
        // Otherwise node is leaf and should be returned.
        return node;
        
    }
    private MonteCarloNode ExpandNode(MonteCarloNode node)
    {
        // Populate node's children.
        // Consider rewriting to only add one child per expansion step.
        Move[] legalMoves = node.Board.GetLegalMoves();
        //List<Move> unexploredMoves = new List<Move>();

        foreach (Move move in legalMoves) {
            Board childBoard=CloneBoard(node.Board);
            childBoard.MakeMove(move);
            node.Children.Add(new MonteCarloNode(move,childBoard,node));
        }

        return node.Children[random.Next(node.Children.Count)];
        
    }
    private bool SimulateRandomPlayoutWin(Board board, bool playerIsWhite)
    {
        while (!board.IsDraw() && !board.IsInCheckmate())
        {
            board.MakeMove(GetRandomLegalMove(board));
        }

        return board.IsInCheckmate() && !(playerIsWhite^board.IsWhiteToMove); // Did we win?
    }
    private void Backpropagate(MonteCarloNode node, double result)
    {
        // if(result>0) System.Console.WriteLine("Board Fen: "+node.Board.GetFenString()+", Action: "+node.Action+", visited:"+node.Visits+", wins!");
        while (node != null)
        {
            node.Visits++;
            node.Wins += result;
            node = node.Parent;
        }
    }
    private Move GetRandomLegalMove(Board board)
    {
        // Helper method to get random move
        Move[] legalMoves = board.GetLegalMoves();
        int randomIndex=random.Next(legalMoves.Length);
        if(randomIndex>legalMoves.Length) System.Console.WriteLine(randomIndex+" IS GREATER THAN "+legalMoves);
        if(randomIndex<0) System.Console.WriteLine(randomIndex+" IS NEGATIVE"+legalMoves);
        if(legalMoves.Length==0) System.Console.WriteLine("NO LEGAL MOVES YOU FUCKASS");
        return legalMoves[randomIndex];
    }
    private Board CloneBoard(Board board)
    {
        // Sebastian says this is slow, but could it possibly be slower for rollout than spamming UndoMove()?
        return Board.CreateBoardFromFEN(board.GetFenString());
    }
    
}
public class MonteCarloNode
{
    public int Visits { get; set; }
    public double Wins { get; set; }
    public List<MonteCarloNode> Children { get; set; }
    public MonteCarloNode Parent { get; set; }
    public Move Action { get; set; }
    public Board Board { get; set; }

    public MonteCarloNode(Move action, Board board, MonteCarloNode parent = null)
    {

        Action = action;
        Parent = parent;
        Board = board;
        Children = new List<MonteCarloNode>();
        Visits = 0;
        Wins = 0;
    }

    public double UCTValue()
    {
        // This is almost straight out of the textbook, so to speak. Only thing is avoiding the division by zero due to populating all children during expansion.
        const double explorationConstant = 1.41; // sqrt(2)
        
        return Visits==0? double.MaxValue: (Wins / Visits) + explorationConstant * Math.Sqrt(Math.Log(Parent.Visits) / Visits);
    }
}