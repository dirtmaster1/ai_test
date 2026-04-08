// 2D Grid Scene with Movable Circle using Three.js
class GridScene {
    constructor(containerId = 'gameContainer') {
        // Grid settings
        this.gridWidth = 100;
        this.gridHeight = 100;
        this.viewWidth = 10;
        this.viewHeight = 10;
        this.cellSize = 64;
        
        // Create container
        this.container = document.getElementById(containerId);
        if (!this.container) {
            this.container = document.createElement('div');
            this.container.id = containerId;
            document.body.appendChild(this.container);
        }
        
        // Scene, Camera, Renderer setup
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x1a1a1a);
        
        const viewW = this.viewWidth * this.cellSize;
        const viewH = this.viewHeight * this.cellSize;
        
        // Orthographic camera for 2D-like view (shows 10x10 viewport)
        this.camera = new THREE.OrthographicCamera(
            -viewW / 2,
            viewW / 2,
            viewH / 2,
            -viewH / 2,
            0.1,
            1000
        );
        this.camera.position.z = 500;
        
        // Renderer
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setSize(viewW, viewH);
        this.renderer.setPixelRatio(window.devicePixelRatio);
        this.container.appendChild(this.renderer.domElement);
        
        // Blue character properties
        this.circle = {
            gridX: 50,
            gridY: 50,
            radius: 20,
            mesh: null,
            color: 'blue',
            baseColorHex: 0x0066ff,
            facing: 'right',
            directionPointer: null,
            hitPoints: 10,
            hitAnimEndTime: 0,
            isDead: false,
            fadeFrames: 0,
            removedFromScene: false
        };
        
        // Red character properties
        this.redCircle = {
            gridX: 45,
            gridY: 45,
            radius: 20,
            mesh: null,
            color: 'red',
            baseColorHex: 0xff3333,
            facing: 'right',
            directionPointer: null,
            hitPoints: 10,
            hitAnimEndTime: 0,
            isDead: false,
            fadeFrames: 0,
            removedFromScene: false
        };
        
        // Turn system
        this.currentTurn = 'blue'; // 'blue' or 'red'
        this.movesThisTurn = 0;
        this.maxMovesPerTurn = 5;
        this.turnInfo = null;
        this.redMoveTimer = 0;
        this.isGameOver = false;
        this.gameOutcome = null;
        this.victoryStartTime = 0;
        this.victoryFadeDurationMs = 3000;
        this.restartTriggered = false;
        
        // Obstacles
        this.obstacles = this.generateObstacles();
        
        // Input handling
        this.keysPressed = {};
        this.setupInputListeners();
        
        // Setup scene
        this.setupGrid();
        this.setupObstacles();
        this.setupCircle();
        this.setupRedCircle();
        this.setupUI();
        this.setupAttackListener();
        this.updateCamera();
        
        // Start animation loop
        this.animate();
    }
    
    setupGrid() {
        // Create grid lines using line segments
        const gridMaterial = new THREE.LineBasicMaterial({ color: 0x444444 });
        const gridGroup = new THREE.Group();
        
        const width = this.gridWidth * this.cellSize;
        const height = this.gridHeight * this.cellSize;
        
        // Vertical lines
        for (let x = 0; x <= this.gridWidth; x++) {
            const points = [
                new THREE.Vector3(x * this.cellSize - width / 2, height / 2, 0),
                new THREE.Vector3(x * this.cellSize - width / 2, -height / 2, 0)
            ];
            const geometry = new THREE.BufferGeometry().setFromPoints(points);
            const line = new THREE.Line(geometry, gridMaterial);
            gridGroup.add(line);
        }
        
        // Horizontal lines
        for (let y = 0; y <= this.gridHeight; y++) {
            const points = [
                new THREE.Vector3(-width / 2, y * this.cellSize - height / 2, 0),
                new THREE.Vector3(width / 2, y * this.cellSize - height / 2, 0)
            ];
            const geometry = new THREE.BufferGeometry().setFromPoints(points);
            const line = new THREE.Line(geometry, gridMaterial);
            gridGroup.add(line);
        }
        
        this.scene.add(gridGroup);
    }
    
    generateObstacles() {
        const obstacles = [];
        const numObstacles = 700;
        const startingPositions = [
            { x: this.circle.gridX, y: this.circle.gridY },
            { x: this.redCircle.gridX, y: this.redCircle.gridY }
        ];
        
        for (let i = 0; i < numObstacles; i++) {
            let x, y, isValid;
            
            // Generate random position that doesn't overlap with circles or other obstacles
            do {
                isValid = true;
                x = Math.floor(Math.random() * this.gridWidth);
                y = Math.floor(Math.random() * this.gridHeight);
                
                // Check if position is already taken
                if (startingPositions.some(pos => pos.x === x && pos.y === y)) {
                    isValid = false;
                }
                if (obstacles.some(obs => obs.x === x && obs.y === y)) {
                    isValid = false;
                }
            } while (!isValid);
            
            obstacles.push({ x, y });
        }
        
        return obstacles;
    }
    
    setupObstacles() {
        // Create visual representation of obstacles
        const width = this.gridWidth * this.cellSize;
        const height = this.gridHeight * this.cellSize;
        
        this.obstacles.forEach(obstacle => {
            // Create a small square to represent the obstacle
            const geometry = new THREE.PlaneGeometry(this.cellSize - 4, this.cellSize - 4);
            const material = new THREE.MeshBasicMaterial({ color: 0x004488 }); // Darker blue
            const mesh = new THREE.Mesh(geometry, material);
            
            const x = (obstacle.x * this.cellSize + this.cellSize / 2) - width / 2;
            const y = (height / 2) - (obstacle.y * this.cellSize + this.cellSize / 2);
            
            mesh.position.set(x, y, -1); // Slightly behind circles
            this.scene.add(mesh);
        });
    }
    
    isObstacle(gridX, gridY) {
        return this.obstacles.some(obs => obs.x === gridX && obs.y === gridY);
    }
    
    setupCircle() {
        // Create blue circle
        const geometry = new THREE.CircleGeometry(this.circle.radius, 32);
        const material = new THREE.MeshBasicMaterial({ color: 0x0066ff });
        this.circle.mesh = new THREE.Mesh(geometry, material);

        this.circle.directionPointer = this.createDirectionPointer(0xffffff);
        this.circle.mesh.add(this.circle.directionPointer);
        this.updateCharacterFacing(this.circle, this.circle.facing);
        
        // Add circle outline
        const outlineGeometry = new THREE.CircleGeometry(this.circle.radius, 32);
        const outlineMaterial = new THREE.LineBasicMaterial({ color: 0x0044cc, linewidth: 2 });
        const outline = new THREE.LineSegments(outlineGeometry, outlineMaterial);
        outline.position.copy(this.circle.mesh.position);
        this.circle.mesh.add(outline);
        
        this.updateCirclePosition();
        this.scene.add(this.circle.mesh);
    }
    
    setupRedCircle() {
        // Create red circle
        const geometry = new THREE.CircleGeometry(this.redCircle.radius, 32);
        const material = new THREE.MeshBasicMaterial({ color: 0xff3333 });
        this.redCircle.mesh = new THREE.Mesh(geometry, material);

        this.redCircle.directionPointer = this.createDirectionPointer(0xffffff);
        this.redCircle.mesh.add(this.redCircle.directionPointer);
        this.updateCharacterFacing(this.redCircle, this.redCircle.facing);
        
        // Add circle outline
        const outlineGeometry = new THREE.CircleGeometry(this.redCircle.radius, 32);
        const outlineMaterial = new THREE.LineBasicMaterial({ color: 0xcc0000, linewidth: 2 });
        const outline = new THREE.LineSegments(outlineGeometry, outlineMaterial);
        outline.position.copy(this.redCircle.mesh.position);
        this.redCircle.mesh.add(outline);
        
        this.updateRedCirclePosition();
        this.scene.add(this.redCircle.mesh);
    }
    
    updateCirclePosition() {
        const width = this.gridWidth * this.cellSize;
        const height = this.gridHeight * this.cellSize;
        
        const x = (this.circle.gridX * this.cellSize + this.cellSize / 2) - width / 2;
        const y = (height / 2) - (this.circle.gridY * this.cellSize + this.cellSize / 2);
        
        this.circle.mesh.position.set(x, y, 0);
    }
    
    updateRedCirclePosition() {
        const width = this.gridWidth * this.cellSize;
        const height = this.gridHeight * this.cellSize;
        
        const x = (this.redCircle.gridX * this.cellSize + this.cellSize / 2) - width / 2;
        const y = (height / 2) - (this.redCircle.gridY * this.cellSize + this.cellSize / 2);
        
        this.redCircle.mesh.position.set(x, y, 0);
    }

    updateCamera() {
        const width = this.gridWidth * this.cellSize;
        const height = this.gridHeight * this.cellSize;
        const x = (this.circle.gridX * this.cellSize + this.cellSize / 2) - width / 2;
        const y = (height / 2) - (this.circle.gridY * this.cellSize + this.cellSize / 2);
        this.camera.position.x = x;
        this.camera.position.y = y;
    }

    createDirectionPointer(color) {
        const pointerShape = new THREE.Shape();
        pointerShape.moveTo(8, 0);
        pointerShape.lineTo(-6, 5);
        pointerShape.lineTo(-6, -5);
        pointerShape.closePath();

        const pointerGeometry = new THREE.ShapeGeometry(pointerShape);
        const pointerMaterial = new THREE.MeshBasicMaterial({ color });
        const pointerMesh = new THREE.Mesh(pointerGeometry, pointerMaterial);
        pointerMesh.position.set(0, 0, 1);

        return pointerMesh;
    }

    updateCharacterFacing(character, facing) {
        if (!character || !character.directionPointer) {
            return;
        }

        character.facing = facing;

        const rotationByDirection = {
            right: 0,
            down: -Math.PI / 2,
            left: Math.PI,
            up: Math.PI / 2
        };

        character.directionPointer.rotation.z = rotationByDirection[facing] ?? 0;
    }
    
    setupUI() {
        // Create side panel for all character info
        const sidePanel = document.createElement('div');
        sidePanel.id = 'sidePanel';
        sidePanel.style.position = 'absolute';
        sidePanel.style.left = '-200px';
        sidePanel.style.top = '50%';
        sidePanel.style.transform = 'translateY(-50%)';
        sidePanel.style.pointerEvents = 'none';
        sidePanel.style.color = '#fff';
        sidePanel.style.fontFamily = 'Arial, sans-serif';
        sidePanel.style.fontSize = '14px';
        sidePanel.style.lineHeight = '1.6';
        
        // Blue character section
        const blueSection = document.createElement('div');
        blueSection.style.marginBottom = '30px';
        
        const blueTitle = document.createElement('div');
        blueTitle.style.fontSize = '16px';
        blueTitle.style.fontWeight = 'bold';
        blueTitle.style.color = '#0066ff';
        blueTitle.textContent = 'Blue (Player)';
        this.blueTitle = blueTitle;
        blueSection.appendChild(blueTitle);
        
        this.bluePositionText = document.createElement('div');
        this.bluePositionText.id = 'bluePosition';
        blueSection.appendChild(this.bluePositionText);
        
        this.blueHPText = document.createElement('div');
        this.blueHPText.id = 'blueHP';
        blueSection.appendChild(this.blueHPText);
        
        this.blueTurnInfo = document.createElement('div');
        this.blueTurnInfo.id = 'blueTurnInfo';
        this.blueTurnInfo.style.fontWeight = 'bold';
        this.blueTurnInfo.style.marginTop = '5px';
        blueSection.appendChild(this.blueTurnInfo);
        this.blueSection = blueSection;
        
        sidePanel.appendChild(blueSection);
        
        // Red character section
        const redSection = document.createElement('div');
        
        const redTitle = document.createElement('div');
        redTitle.style.fontSize = '16px';
        redTitle.style.fontWeight = 'bold';
        redTitle.style.color = '#ff3333';
        redTitle.textContent = 'Red (AI)';
        this.redTitle = redTitle;
        redSection.appendChild(redTitle);
        
        this.redPositionText = document.createElement('div');
        this.redPositionText.id = 'redPosition';
        redSection.appendChild(this.redPositionText);
        
        this.redHPText = document.createElement('div');
        this.redHPText.id = 'redHP';
        redSection.appendChild(this.redHPText);
        
        this.redTurnInfo = document.createElement('div');
        this.redTurnInfo.id = 'redTurnInfo';
        this.redTurnInfo.style.fontWeight = 'bold';
        this.redTurnInfo.style.marginTop = '5px';
        redSection.appendChild(this.redTurnInfo);
        this.redSection = redSection;
        
        sidePanel.appendChild(redSection);

        this.victoryText = document.createElement('div');
        this.victoryText.id = 'victoryText';
        this.victoryText.textContent = 'Victory!';
        this.victoryText.style.position = 'absolute';
        this.victoryText.style.top = '50%';
        this.victoryText.style.left = '50%';
        this.victoryText.style.transform = 'translate(-50%, -50%)';
        this.victoryText.style.fontFamily = 'Arial, sans-serif';
        this.victoryText.style.fontSize = '96px';
        this.victoryText.style.fontWeight = '900';
        this.victoryText.style.letterSpacing = '4px';
        this.victoryText.style.color = '#ffd700';
        this.victoryText.style.textShadow = '0 0 16px rgba(255, 215, 0, 0.6), 0 0 32px rgba(255, 140, 0, 0.45)';
        this.victoryText.style.pointerEvents = 'none';
        this.victoryText.style.opacity = '0';
        this.victoryText.style.display = 'none';
        this.victoryText.style.zIndex = '20';
        
        this.container.style.position = 'relative';
        this.container.appendChild(sidePanel);
        this.container.appendChild(this.victoryText);
    }
    
    setupInputListeners() {
        document.addEventListener('keydown', (e) => {
            const key = e.key.toUpperCase();

            if (this.isGameOver) return;
            
            // Only accept input during blue's turn
            if (this.currentTurn !== 'blue') return;
            
            // Only trigger movement on key press, not on hold
            if (!this.keysPressed[key]) {
                this.keysPressed[key] = true;
                this.handleMovement(key);
            }
        });
        
        document.addEventListener('keyup', (e) => {
            const key = e.key.toUpperCase();
            this.keysPressed[key] = false;
        });
    }
    
    setupAttackListener() {
        // Create raycaster and mouse vector for click detection
        this.raycaster = new THREE.Raycaster();
        this.mouse = new THREE.Vector2();
        
        this.renderer.domElement.addEventListener('click', (event) => {
            // Only allow attacks during blue's turn
            if (this.isGameOver) return;
            if (this.currentTurn !== 'blue') return;
            if (this.circle.isDead || this.redCircle.isDead || !this.redCircle.mesh) return;
            
            // Calculate mouse position in normalized device coordinates
            const rect = this.renderer.domElement.getBoundingClientRect();
            this.mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
            this.mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;
            
            // Update the picking ray with the camera and mouse position
            this.raycaster.setFromCamera(this.mouse, this.camera);
            
            // Calculate objects intersecting the picking ray
            const intersects = this.raycaster.intersectObject(this.redCircle.mesh);
            
            if (intersects.length > 0) {
                this.attackRedCharacter();
            }
        });
    }
    
    handleMovement(key) {
        // Only allow movement during blue's turn
        if (this.isGameOver || this.currentTurn !== 'blue' || this.movesThisTurn >= this.maxMovesPerTurn || this.circle.isDead) {
            return;
        }
        
        let newX = this.circle.gridX;
        let newY = this.circle.gridY;
        
        // Calculate new position based on key press
        switch (key) {
            case 'W':
                newY = Math.max(0, this.circle.gridY - 1);
                break;
            case 'S':
                newY = Math.min(this.gridHeight - 1, this.circle.gridY + 1);
                break;
            case 'A':
                newX = Math.max(0, this.circle.gridX - 1);
                break;
            case 'D':
                newX = Math.min(this.gridWidth - 1, this.circle.gridX + 1);
                break;
            default:
                return; // No valid move
        }
        
        // Check if new position is occupied by red circle
        if (newX === this.redCircle.gridX && newY === this.redCircle.gridY) {
            return; // Can't move to occupied cell
        }
        
        // Check if new position is an obstacle
        if (this.isObstacle(newX, newY)) {
            return; // Can't move into obstacle
        }
        
        const dx = newX - this.circle.gridX;
        const dy = newY - this.circle.gridY;

        this.circle.gridX = newX;
        this.circle.gridY = newY;
        if (dx > 0) {
            this.updateCharacterFacing(this.circle, 'right');
        } else if (dx < 0) {
            this.updateCharacterFacing(this.circle, 'left');
        } else if (dy > 0) {
            this.updateCharacterFacing(this.circle, 'down');
        } else if (dy < 0) {
            this.updateCharacterFacing(this.circle, 'up');
        }
        
        this.updateCirclePosition();
        this.updateCamera();
        this.movesThisTurn++;
        
        // Switch turn if blue has made 5 moves
        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }
    }
    
    switchTurn() {
        this.currentTurn = this.currentTurn === 'blue' ? 'red' : 'blue';
        this.movesThisTurn = 0;
    }
    
    attackRedCharacter() {
        // Check if blue has at least 3 moves available
        const movesLeft = this.maxMovesPerTurn - this.movesThisTurn;
        if (movesLeft < 3 || this.circle.isDead || this.redCircle.isDead) {
            return; // Not enough moves to attack
        }
        
        // Apply damage
        const attackDamage = 5;
        this.redCircle.hitPoints -= attackDamage;
        this.playHitAnimation(this.redCircle);
        
        // Ensure hit points don't go below 0
        if (this.redCircle.hitPoints < 0) {
            this.redCircle.hitPoints = 0;
        }

        if (this.redCircle.hitPoints <= 0) {
            this.markCharacterDead(this.redCircle);
        }
        
        // Consume 3 moves
        this.movesThisTurn += 3;
        
        // Switch turn if blue has made 5 moves
        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }
    }

    playHitAnimation(character) {
        if (!character || !character.mesh || character.removedFromScene) {
            return;
        }

        character.hitAnimEndTime = performance.now() + 1000;
    }

    updateHitAnimation(character, nowMs) {
        if (!character || !character.mesh || character.removedFromScene) {
            return;
        }

        if (!character.hitAnimEndTime || nowMs >= character.hitAnimEndTime) {
            character.mesh.scale.set(1, 1, 1);
            character.mesh.material.color.setHex(character.baseColorHex);
            character.hitAnimEndTime = 0;
            return;
        }

        const durationMs = 1000;
        const remainingMs = character.hitAnimEndTime - nowMs;
        const progress = 1 - (remainingMs / durationMs);

        // Pulse size quickly and taper out over the 1 second window.
        const envelope = 1 - progress;
        const pulse = 1 + (Math.sin(progress * Math.PI * 10) * 0.18 * envelope);
        character.mesh.scale.set(pulse, pulse, 1);

        // Flash toward white while preserving base color identity.
        const flashAmount = Math.abs(Math.sin(progress * Math.PI * 12)) * 0.65 * envelope;
        const baseColor = new THREE.Color(character.baseColorHex);
        const hitColor = baseColor.clone().lerp(new THREE.Color(0xffffff), flashAmount);
        character.mesh.material.color.copy(hitColor);
    }
    
    moveRedCircle() {
        if (this.isGameOver || this.currentTurn !== 'red' || this.movesThisTurn >= this.maxMovesPerTurn || this.redCircle.isDead) {
            return;
        }

        // AI prefers attacking when it has enough moves for a hit.
        if (this.redAIAttackBlueCharacter()) {
            return;
        }
        
        // Calculate direction towards blue circle
        const dx = this.circle.gridX - this.redCircle.gridX;
        const dy = this.circle.gridY - this.redCircle.gridY;
        
        // Determine the best move to get closer
        const absDx = Math.abs(dx);
        const absDy = Math.abs(dy);
        
        let newX = this.redCircle.gridX;
        let newY = this.redCircle.gridY;
        
        // Prioritize moving along the axis with greater distance
        if (absDx > absDy) {
            // Move horizontally first
            if (dx > 0) {
                newX = Math.min(this.gridWidth - 1, this.redCircle.gridX + 1); // Move right
            } else if (dx < 0) {
                newX = Math.max(0, this.redCircle.gridX - 1); // Move left
            }
        } else {
            // Move vertically first
            if (dy > 0) {
                newY = Math.min(this.gridHeight - 1, this.redCircle.gridY + 1); // Move down
            } else if (dy < 0) {
                newY = Math.max(0, this.redCircle.gridY - 1); // Move up
            }
        }
        
        // Check if new position is occupied by blue circle
        if (newX === this.circle.gridX && newY === this.circle.gridY) {
            // Can't move there, try alternative move
            // If we were trying to move horizontally, try vertical instead
            if (absDx > absDy) {
                newX = this.redCircle.gridX; // Reset X
                if (dy > 0) {
                    newY = Math.min(this.gridHeight - 1, this.redCircle.gridY + 1);
                } else if (dy < 0) {
                    newY = Math.max(0, this.redCircle.gridY - 1);
                }
            } else {
                // If we were trying to move vertically, try horizontal instead
                newY = this.redCircle.gridY; // Reset Y
                if (dx > 0) {
                    newX = Math.min(this.gridWidth - 1, this.redCircle.gridX + 1);
                } else if (dx < 0) {
                    newX = Math.max(0, this.redCircle.gridX - 1);
                }
            }
            
            // Check if alternative move is also blocked
            if ((newX === this.circle.gridX && newY === this.circle.gridY) || this.isObstacle(newX, newY)) {
                // Can't move anywhere this turn (blocked in both directions)
                this.movesThisTurn++;
                if (this.movesThisTurn >= this.maxMovesPerTurn) {
                    this.switchTurn();
                }
                return;
            }
        } else if (this.isObstacle(newX, newY)) {
            // Hit an obstacle, try alternative move
            if (absDx > absDy) {
                newX = this.redCircle.gridX; // Reset X
                if (dy > 0) {
                    newY = Math.min(this.gridHeight - 1, this.redCircle.gridY + 1);
                } else if (dy < 0) {
                    newY = Math.max(0, this.redCircle.gridY - 1);
                }
            } else {
                newY = this.redCircle.gridY; // Reset Y
                if (dx > 0) {
                    newX = Math.min(this.gridWidth - 1, this.redCircle.gridX + 1);
                } else if (dx < 0) {
                    newX = Math.max(0, this.redCircle.gridX - 1);
                }
            }
            
            // Check if alternative move is also blocked
            if ((newX === this.circle.gridX && newY === this.circle.gridY) || this.isObstacle(newX, newY)) {
                // Can't move anywhere this turn
                this.movesThisTurn++;
                if (this.movesThisTurn >= this.maxMovesPerTurn) {
                    this.switchTurn();
                }
                return;
            }
        }
        
        const redDx = newX - this.redCircle.gridX;
        const redDy = newY - this.redCircle.gridY;

        this.redCircle.gridX = newX;
        this.redCircle.gridY = newY;

        if (redDx > 0) {
            this.updateCharacterFacing(this.redCircle, 'right');
        } else if (redDx < 0) {
            this.updateCharacterFacing(this.redCircle, 'left');
        } else if (redDy > 0) {
            this.updateCharacterFacing(this.redCircle, 'down');
        } else if (redDy < 0) {
            this.updateCharacterFacing(this.redCircle, 'up');
        }
        
        this.updateRedCirclePosition();
        this.movesThisTurn++;
        
        // Switch turn if red has made 5 moves
        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }
    }

    redAIAttackBlueCharacter() {
        if (this.isGameOver || this.currentTurn !== 'red' || this.circle.isDead || this.redCircle.isDead) {
            return false;
        }

        const dx = Math.abs(this.circle.gridX - this.redCircle.gridX);
        const dy = Math.abs(this.circle.gridY - this.redCircle.gridY);
        const isInAttackRange = dx <= 1 && dy <= 1;
        if (!isInAttackRange) {
            return false;
        }

        const movesLeft = this.maxMovesPerTurn - this.movesThisTurn;
        if (movesLeft < 3) {
            return false;
        }

        const attackDamage = 5;
        this.circle.hitPoints -= attackDamage;
        this.playHitAnimation(this.circle);

        if (this.circle.hitPoints < 0) {
            this.circle.hitPoints = 0;
        }

        if (this.circle.hitPoints <= 0) {
            this.markCharacterDead(this.circle);
        }

        this.movesThisTurn += 3;

        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }

        return true;
    }

    markCharacterDead(character) {
        if (character.isDead) {
            return;
        }

        character.isDead = true;
        character.fadeFrames = 0;

        if (character.hitPoints < 0) {
            character.hitPoints = 0;
        }

        // If dead character had the active turn, pass turn immediately.
        if ((character === this.circle && this.currentTurn === 'blue') ||
            (character === this.redCircle && this.currentTurn === 'red')) {
            this.switchTurn();
        }
    }

    fadeAndRemoveCharacter(character) {
        if (!character.isDead || !character.mesh || character.removedFromScene) {
            return;
        }

        const fadeDurationFrames = 60;
        character.fadeFrames += 1;

        const opacity = Math.max(0, 1 - (character.fadeFrames / fadeDurationFrames));
        character.mesh.material.transparent = true;
        character.mesh.material.opacity = opacity;

        // Fade child visuals (outline) as well.
        character.mesh.children.forEach((child) => {
            if (child.material) {
                child.material.transparent = true;
                child.material.opacity = opacity;
            }
        });

        if (character.fadeFrames >= fadeDurationFrames) {
            this.scene.remove(character.mesh);
            character.mesh = null;
            character.removedFromScene = true;
        }
    }

    getAICharacters() {
        return [this.redCircle];
    }

    areAllAICharactersDead() {
        const aiCharacters = this.getAICharacters();
        return aiCharacters.length > 0 && aiCharacters.every((character) => character.isDead);
    }

    startVictorySequence() {
        if (this.isGameOver) {
            return;
        }

        this.isGameOver = true;
        this.gameOutcome = 'victory';
        this.victoryStartTime = performance.now();
        this.victoryText.textContent = 'Victory!';
        this.victoryText.style.color = '#ffd700';
        this.victoryText.style.textShadow = '0 0 16px rgba(255, 215, 0, 0.6), 0 0 32px rgba(255, 140, 0, 0.45)';
        this.victoryText.style.display = 'block';
        this.victoryText.style.opacity = '1';
    }

    startGameOverSequence() {
        if (this.isGameOver) {
            return;
        }

        this.isGameOver = true;
        this.gameOutcome = 'defeat';
        this.victoryStartTime = performance.now();
        this.victoryText.textContent = 'Game Over';
        this.victoryText.style.color = '#ff4d4d';
        this.victoryText.style.textShadow = '0 0 16px rgba(255, 77, 77, 0.6), 0 0 32px rgba(128, 0, 0, 0.45)';
        this.victoryText.style.display = 'block';
        this.victoryText.style.opacity = '1';
    }

    updateVictorySequence() {
        if (!this.isGameOver) {
            return;
        }

        const elapsedMs = performance.now() - this.victoryStartTime;
        const progress = Math.min(1, elapsedMs / this.victoryFadeDurationMs);
        this.victoryText.style.opacity = String(1 - progress);

        if (progress >= 1 && !this.restartTriggered) {
            this.restartTriggered = true;
            window.location.reload();
        }
    }
    
    update() {
        const nowMs = performance.now();

        // Update blue character info
        this.bluePositionText.textContent = `Position: (${this.circle.gridX}, ${this.circle.gridY})`;
        this.blueHPText.textContent = `HP: ${this.circle.hitPoints}`;
        
        // Update red character info
        this.redPositionText.textContent = `Position: (${this.redCircle.gridX}, ${this.redCircle.gridY})`;
        this.redHPText.textContent = `HP: ${this.redCircle.hitPoints}`;

        // Dead characters are grayed out and show status.
        const deadColor = '#666666';
        const aliveInfoColor = '#ffffff';

        this.blueSection.style.opacity = this.circle.isDead ? '0.65' : '1';
        this.redSection.style.opacity = this.redCircle.isDead ? '0.65' : '1';

        this.blueTitle.style.color = this.circle.isDead ? deadColor : '#0066ff';
        this.redTitle.style.color = this.redCircle.isDead ? deadColor : '#ff3333';

        this.bluePositionText.style.color = this.circle.isDead ? deadColor : aliveInfoColor;
        this.blueHPText.style.color = this.circle.isDead ? deadColor : aliveInfoColor;
        this.redPositionText.style.color = this.redCircle.isDead ? deadColor : aliveInfoColor;
        this.redHPText.style.color = this.redCircle.isDead ? deadColor : aliveInfoColor;

        // Update turn/dead indicators.
        const movesLeft = this.maxMovesPerTurn - this.movesThisTurn;

        if (this.circle.isDead) {
            this.blueTurnInfo.textContent = 'Status: DEAD';
            this.blueTurnInfo.style.color = deadColor;
        } else if (this.currentTurn === 'blue') {
            this.blueTurnInfo.textContent = `Status: ${movesLeft} moves left`;
            this.blueTurnInfo.style.color = '#0066ff';
        } else {
            this.blueTurnInfo.textContent = 'Status: Waiting';
            this.blueTurnInfo.style.color = '#9aa0aa';
        }

        if (this.redCircle.isDead) {
            this.redTurnInfo.textContent = 'Status: DEAD';
            this.redTurnInfo.style.color = deadColor;
        } else if (this.currentTurn === 'red') {
            this.redTurnInfo.textContent = `Status: ${movesLeft} moves left`;
            this.redTurnInfo.style.color = '#ff3333';
        } else {
            this.redTurnInfo.textContent = 'Status: Waiting';
            this.redTurnInfo.style.color = '#9aa0aa';
        }

        this.fadeAndRemoveCharacter(this.circle);
        this.fadeAndRemoveCharacter(this.redCircle);
        this.updateHitAnimation(this.circle, nowMs);
        this.updateHitAnimation(this.redCircle, nowMs);

        if (!this.isGameOver && this.circle.isDead) {
            this.startGameOverSequence();
        }

        if (!this.isGameOver && this.areAllAICharactersDead()) {
            this.startVictorySequence();
        }

        if (this.isGameOver) {
            this.updateVictorySequence();
            return;
        }
        
        // Control red circle movement with a timer (every 30 frames = ~500ms at 60fps)
        this.redMoveTimer++;
        if (this.redMoveTimer >= 30) {
            this.moveRedCircle();
            this.redMoveTimer = 0;
        }
    }
    
    animate() {
        this.update();
        this.updateCamera();
        this.renderer.render(this.scene, this.camera);
        requestAnimationFrame(() => this.animate());
    }
}

// Initialize the scene when the page loads
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        new GridScene('gameContainer');
    });
} else {
    new GridScene('gameContainer');
}
