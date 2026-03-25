# Dark Dungeon

A 3D interactive game built with Three.js featuring a dark atmospheric corridor with an interactive title screen that transitions to a rotating blue cube.

## Project Layout

```
ai_test/
├── index.html              # Main HTML entry point
├── game.js                 # Core Three.js game logic and scenes
├── script.js               # Additional game scripts
├── server.js               # Node.js HTTP server
├── character_tileset.png   # Character sprite sheet
├── enemy_tileset.png       # Enemy sprite sheet
└── README.md               # This file
```

## Getting Started

### Prerequisites
- Node.js installed on your system

### Installation

1. Clone or navigate to the repository:
```bash
cd ai_test
```

2. Install dependencies (if any):
```bash
npm install
```

### Running the Game

1. Start the server:
```bash
node server.js
```

2. Open your browser and navigate to:
```
http://localhost:8000
```

3. You should see the "Dark Dungeon" title screen. Click on it to enter the game and see the rotating blue cube.

## Game Features

- **Title Screen**: Dark atmospheric introduction with gothic font styling
- **3D Scene**: Built with Three.js WebGL renderer
- **Interactive Transitions**: Click to transition from the corridor scene to the cube scene
- **Dynamic Lighting**: Multiple point lights and directional lighting effects
- **Responsive Design**: Adapts to window resizing