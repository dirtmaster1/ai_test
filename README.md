# Dark Dungeon Tactics

A browser-based tactical combat prototype built with Three.js and plain JavaScript. The public root route now opens directly into the tactical battle, with a lightweight intro overlay and a fully client-side combat scene.

## Current Experience

- `/` is the main public entry point and loads the tactical game directly.
- The landing overlay explains the session, then dismisses into the live combat board.
- Combat is turn-based with player and AI-controlled characters, abilities, buffs, healing, and a floating combat log.
- Everything runs in the browser; `server.js` is only a simple local static file server for development.

## Project Layout

```
ai_test/
├── index.html          # Main public entry point for the tactical game
├── grid_scene.html     # Alternate direct entry to the tactical scene
├── grid_scene.js       # Main scene orchestrator and combat flow
├── character.js        # Character definitions, stats, abilities, and party setup
├── ai.js               # Enemy AI behavior and tactical movement/targeting
├── graphics.js         # Rendering helpers, projectiles, effects, and scene visuals
├── ui.js               # HUD, overlays, inventory modal, and combat log UI
├── map_generator.js    # Dungeon map generation
├── server.js           # Local static development server
├── game.js             # Legacy compatibility redirect shim
├── script.js           # Older experimental Three.js script
└── README.md           # Project documentation
```

## Running Locally

### Prerequisites

- Node.js installed on your system

### Start the Local Server

1. Open the repo folder:

```bash
cd ai_test
```

2. Start the local file server:

```bash
node server.js
```

3. Open the game in your browser:

```text
http://localhost:8000/
```

## Controls

- `W`, `A`, `S`, `D`: move the active character
- Mouse click: target enemies or allies with the currently selected action
- `Space`: end the current character's turn

## Gameplay Notes

- Each living unit acts individually in initiative order.
- Player characters expose their abilities in the HUD.
- AI characters use the same combat systems as the player side.
- The floating combat log records attacks, spells, healing, and buffs.
- The current experience is best on desktop with keyboard and mouse.

## Deployment Fit

This project is a good fit for static hosting platforms such as Cloudflare Pages, Netlify, or GitHub Pages. No application backend is required for the current gameplay loop.