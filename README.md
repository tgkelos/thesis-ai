# thesis-ai
Ai agent solver for custom turn-based game using Minimax +TDL


# Auto-Battler AI – Minimax and Temporal-Difference Learning

This repository contains the source code for a turn-based auto-battler game and a reinforcement learning agent trained via Minimax search and Temporal-Difference Learning (TD Learning).

The AI agent learns to play the game purely through self-play, using a linear evaluation function over handcrafted features. The goal is to produce a fast, interpretable, and reproducible agent without requiring deep networks or GPUs.

## How to Run

To run training, you need the .NET SDK (version 9 or newer). The following minimal example shows how to launch a training session:

```csharp
using System;

class Program
{
    static void Main(string[] args)
    {
        int numGames = 250000;
        var trainer = new TDLearningTrainer();
        trainer.Train(
            numGames: numGames,
            alpha: 0.0005,
            gamma: 0.95,
            depth: 5
        );
    }
}
```

You can customize:
- `numGames`: total self-play games
- `alpha`: learning rate
- `gamma`: discount factor
- `depth`: Minimax search depth

## About the Project

The game is a deterministic, turn-based strategy game played on an 8×8 board. Each player controls 8 units (2 Tanks, 2 Healers, 4 DPS), and the battle unfolds automatically once units are placed. The AI agent uses a linear value function with 16 features and learns by playing against itself using TD(0) or TD(λ).


## Thesis

This codebase accompanies a diploma thesis at the University of Thessaly (2025), titled:

**"Ανάπτυξη AI για Βέλτιστη Στρατηγική σε Turn-Based Game μέσω Προσομοίωσης και Machine Learning"**  
Tryfon Gkelos

For more details or reproduction of experiments, refer to the thesis document or contact the author.


