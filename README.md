# Project Hazard

Project Hazard is a story based 2D action RPG created and developed with the game creation tool Unity using C#. The games core features include exploration, melee and timed combat, enemy AI, boss encounters, character interactions, and multi-scene progression.

### [Play Project Hazard](https://projectsave-68b6b.web.app/index.html)

The game is played directly on your local brwoser. Open the Guide tab on the website to view gameplay demonstrations, controls, and feature walkthroughs.

> Loading the Unity WebGL build may take a few moments.

## Features

- Story driven 2D action rpg elements and gameplay
- Pixel drawn player movement and animation systems
- Melee combat and Rigidbody collision based damage
- Enemy chase and attack behavior/patterns
- Boss encounters and health bar systems
- Scene transitions and positional saving
- Browser deployment using Unity WebGL

## Technologies

- Unity
- C#
- Unity 2D Physics
- Unity Animator and animation events
- Object-oriented programming
- Event-driven programming
- Git and GitHub
- Firebase Hosting

## Selected Source Code

The [`src`](src) folder includes a few of the main systems I built for
Project Hazard.

### Player Controls

Handles player movement, physics, character direction, and animations.

### Enemy AI

Controls how enemies detect, follow, and attack the player.

### Combat

Includes scripts for player health, enemy hitboxes, boss attacks, and damage.

### Game Systems

Handles game management, scene changes, and saving the player's position.

## Repository Structure

```text
src/
├── Combat/
├── EnemyAI/
├── PlayerControls/
└── Systems/