// 2D Grid Scene with Movable Circle using Three.js
class GridScene {
    constructor(containerId = 'gameContainer') {
        // Grid settings
        this.gridWidth = 10;
        this.gridHeight = 10;
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
        
        const width = this.gridWidth * this.cellSize;
        const height = this.gridHeight * this.cellSize;
        
        // Orthographic camera for 2D-like view
        this.camera = new THREE.OrthographicCamera(
            -width / 2,
            width / 2,
            height / 2,
            -height / 2,
            0.1,
            1000
        );
        this.camera.position.z = 500;
        
        // Renderer
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setSize(width, height);
        this.renderer.setPixelRatio(window.devicePixelRatio);
        this.container.appendChild(this.renderer.domElement);
        
        // Circle properties
        this.circle = {
            gridX: 5,
            gridY: 5,
            radius: 20,
            mesh: null,
            color: 'blue'
        };
        
        // Red circle properties
        this.redCircle = {
            gridX: 4,
            gridY: 4,
            radius: 20,
            mesh: null,
            color: 'red'
        };
        
        // Turn system
        this.currentTurn = 'blue'; // 'blue' or 'red'
        this.movesThisTurn = 0;
        this.maxMovesPerTurn = 5;
        this.turnInfo = null;
        this.redMoveTimer = 0;
        
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
        // Generate 7 random obstacles (5-10 range, picking 7 for balance)
        const obstacles = [];
        const numObstacles = 7;
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
    
    setupUI() {
        // Create HTML overlay for text
        const uiContainer = document.createElement('div');
        uiContainer.id = 'uiOverlay';
        uiContainer.style.position = 'absolute';
        uiContainer.style.top = '0';
        uiContainer.style.left = '0';
        uiContainer.style.width = '100%';
        uiContainer.style.pointerEvents = 'none';
        uiContainer.style.color = '#fff';
        uiContainer.style.fontFamily = 'Arial, sans-serif';
        uiContainer.style.fontSize = '14px';
        uiContainer.style.padding = '10px';
        
        this.positionText = document.createElement('div');
        this.positionText.id = 'positionText';
        uiContainer.appendChild(this.positionText);
        
        this.turnInfo = document.createElement('div');
        this.turnInfo.id = 'turnInfo';
        this.turnInfo.style.marginTop = '10px';
        this.turnInfo.style.fontSize = '16px';
        this.turnInfo.style.fontWeight = 'bold';
        uiContainer.appendChild(this.turnInfo);
        
        this.container.style.position = 'relative';
        this.container.appendChild(uiContainer);
    }
    
    setupInputListeners() {
        document.addEventListener('keydown', (e) => {
            const key = e.key.toUpperCase();
            
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
    
    handleMovement(key) {
        // Only allow movement during blue's turn
        if (this.currentTurn !== 'blue' || this.movesThisTurn >= this.maxMovesPerTurn) {
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
        
        this.circle.gridX = newX;
        this.circle.gridY = newY;
        
        this.updateCirclePosition();
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
    
    moveRedCircle() {
        if (this.currentTurn !== 'red' || this.movesThisTurn >= this.maxMovesPerTurn) {
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
        
        this.redCircle.gridX = newX;
        this.redCircle.gridY = newY;
        
        this.updateRedCirclePosition();
        this.movesThisTurn++;
        
        // Switch turn if red has made 5 moves
        if (this.movesThisTurn >= this.maxMovesPerTurn) {
            this.switchTurn();
        }
    }
    
    update() {
        // Update UI text
        this.positionText.textContent = `Blue: (${Math.round(this.circle.gridX)}, ${Math.round(this.circle.gridY)}) | Red: (${Math.round(this.redCircle.gridX)}, ${Math.round(this.redCircle.gridY)})`;
        
        // Turn indicator with color
        const turnColor = this.currentTurn === 'blue' ? '#0066ff' : '#ff3333';
        const movesLeft = this.maxMovesPerTurn - this.movesThisTurn;
        this.turnInfo.textContent = `${this.currentTurn.toUpperCase()}'s Turn - ${movesLeft} moves left`;
        this.turnInfo.style.color = turnColor;
        
        // Control red circle movement with a timer (every 30 frames = ~500ms at 60fps)
        this.redMoveTimer++;
        if (this.redMoveTimer >= 30) {
            this.moveRedCircle();
            this.redMoveTimer = 0;
        }
    }
    
    animate() {
        this.update();
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
