// Graphics - rendering, sprites, textures, animations, projectiles, camera
window.GridGraphics = {

    hexToRgba(hex, alpha) {
        const value = hex.replace('#', '');
        const bigint = parseInt(value, 16);
        const r = (bigint >> 16) & 255;
        const g = (bigint >> 8) & 255;
        const b = bigint & 255;
        return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    },

    createSpriteTexture(rows) {
        const canvas = document.createElement('canvas');
        canvas.width = 16;
        canvas.height = 16;
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, 16, 16);
        rows.forEach((row, y) => {
            row.forEach((color, x) => {
                if (color !== null) {
                    ctx.fillStyle = color;
                    ctx.fillRect(x, y, 1, 1);
                }
            });
        });
        const texture = new THREE.CanvasTexture(canvas);
        texture.magFilter = THREE.NearestFilter;
        texture.minFilter = THREE.NearestFilter;
        return texture;
    },

    createPortraitCanvas(rows, accentColor, label) {
        const canvas = document.createElement('canvas');
        canvas.width = 18;
        canvas.height = 18;
        const ctx = canvas.getContext('2d');

        ctx.fillStyle = '#090909';
        ctx.fillRect(0, 0, 18, 18);

        const bgGradient = ctx.createLinearGradient(0, 0, 18, 18);
        bgGradient.addColorStop(0, this.hexToRgba(accentColor, 0.4));
        bgGradient.addColorStop(1, 'rgba(12, 10, 8, 0.94)');
        ctx.fillStyle = bgGradient;
        ctx.fillRect(1, 1, 16, 16);

        ctx.strokeStyle = accentColor;
        ctx.lineWidth = 1;
        ctx.strokeRect(1.5, 1.5, 15, 15);

        const pixelSize = 1;
        const offsetX = 1;
        const offsetY = 1;
        rows.forEach((row, y) => {
            row.forEach((color, x) => {
                if (color !== null) {
                    ctx.fillStyle = color;
                    ctx.fillRect(offsetX + x * pixelSize, offsetY + y * pixelSize, pixelSize, pixelSize);
                }
            });
        });

        return canvas;
    },

    setupGrid() {
        const TILE_RES = 16;
        const canvasW = this.gridWidth * TILE_RES;
        const canvasH = this.gridHeight * TILE_RES;
        const canvas = document.createElement('canvas');
        canvas.width = canvasW;
        canvas.height = canvasH;
        const ctx = canvas.getContext('2d');

        for (let cy = 0; cy < this.gridHeight; cy++) {
            for (let cx = 0; cx < this.gridWidth; cx++) {
                const tile = this.dungeonMap[cy][cx];
                this.drawDungeonTile(ctx, tile, cx * TILE_RES, cy * TILE_RES, TILE_RES, cx, cy);
            }
        }

        const texture = new THREE.CanvasTexture(canvas);
        texture.magFilter = THREE.NearestFilter;
        texture.minFilter = THREE.NearestFilter;

        const worldW = this.gridWidth * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        const geo = new THREE.PlaneGeometry(worldW, worldH);
        const mat = new THREE.MeshBasicMaterial({ map: texture });
        const mesh = new THREE.Mesh(geo, mat);
        mesh.position.z = -2;
        this.scene.add(mesh);
    },

    drawDungeonTile(ctx, tileType, px, py, T, cx, cy) {
        if (tileType === this.TILE_VOID) {
            ctx.fillStyle = '#000000';
            ctx.fillRect(px, py, T, T);
        } else if (tileType === this.TILE_WALL) {
            this.drawWallTile(ctx, px, py, T, cx, cy);
        } else {
            this.drawFloorTile(ctx, px, py, T, cx, cy);
        }
    },

    drawWallTile(ctx, px, py, T, cx, cy) {
        const H = T >> 1;

        ctx.fillStyle = '#0e1218';
        ctx.fillRect(px, py, T, T);

        ctx.fillStyle = '#07080f';
        ctx.fillRect(px, py + H, T, 1);
        if ((cx + cy) % 2 === 0) {
            ctx.fillRect(px + H, py, 1, H);
        } else {
            ctx.fillRect(px + H, py + H + 1, 1, H - 1);
        }

        ctx.fillStyle = 'rgba(100, 140, 220, 0.09)';
        ctx.fillRect(px, py, T, 2);
        ctx.fillStyle = 'rgba(80, 110, 190, 0.05)';
        ctx.fillRect(px, py + 2, 2, H - 2);

        ctx.fillStyle = 'rgba(0, 0, 0, 0.70)';
        ctx.fillRect(px, py + T - 2, T, 2);
        ctx.fillStyle = 'rgba(0, 0, 0, 0.50)';
        ctx.fillRect(px + T - 2, py, 2, T);

        const s = (cx * 1664525 + cy * 214013) >>> 0;
        if (s % 9 < 2) {
            ctx.fillStyle = 'rgba(255,255,255,0.025)';
            ctx.fillRect(px + 1 + (s % (H - 2)), py + 1 + ((s >> 8) % (H - 2)), H - 2, H - 2);
        }
        if (s % 11 === 3) {
            ctx.fillStyle = 'rgba(0,0,0,0.25)';
            ctx.fillRect(px + 1 + (s % (T - 3)), py + 1 + ((s >> 4) % (T - 3)), 2, 2);
        }
    },

    drawFloorTile(ctx, px, py, T, cx, cy) {
        const s = (cx * 1664525 ^ cy * 214013 ^ 0xDEAD) >>> 0;
        const v = s % 5;

        ctx.fillStyle = `rgb(${22 + v * 2},${17 + v},12)`;
        ctx.fillRect(px, py, T, T);

        ctx.fillStyle = 'rgba(0,0,0,0.60)';
        ctx.fillRect(px, py, T, 1);
        ctx.fillRect(px, py, 1, T);

        ctx.fillStyle = 'rgba(255,220,140,0.055)';
        ctx.fillRect(px + 1, py + 1, T - 2, 1);
        ctx.fillRect(px + 1, py + 2, 1, T - 3);

        ctx.fillStyle = 'rgba(0,0,0,0.35)';
        ctx.fillRect(px + 1, py + T - 2, T - 2, 1);
        ctx.fillRect(px + T - 2, py + 1, 1, T - 2);

        if (s % 8 === 0) {
            ctx.fillStyle = 'rgba(0,0,0,0.45)';
            const crackX = px + 2 + (s % (T - 5));
            const crackY = py + 2 + ((s >> 8) % (T - 5));
            ctx.fillRect(crackX, crackY, 4, 1);
            ctx.fillRect(crackX + 3, crackY, 1, 3);
        }

        if (s % 13 === 5) {
            ctx.fillStyle = 'rgba(0,0,0,0.50)';
            ctx.fillRect(px + 2 + (s % (T - 5)), py + 2 + ((s >> 5) % (T - 5)), 2, 2);
        }
    },

    setupCharacters() {
        this.characters.forEach((character) => {
            this.setupCharacterSprite(
                character,
                this.createSpriteTexture(character.spriteRows),
                character.pointerColor
            );
        });
    },

    setupCharacterSprite(character, texture, pointerColor) {
        const size = this.cellSize - 4;
        const geometry = new THREE.PlaneGeometry(size, size);
        const material = new THREE.MeshBasicMaterial({ map: texture, transparent: true, color: 0xffffff });
        character.mesh = new THREE.Mesh(geometry, material);
        character.directionPointer = this.createDirectionPointer(pointerColor);
        character.mesh.add(character.directionPointer);
        this.updateCharacterFacing(character, character.facing);
        this.updateCharacterPosition(character);
        this.scene.add(character.mesh);
    },

    updateCharacterPosition(character) {
        if (!character.mesh) {
            return;
        }

        const { x, y } = this.getWorldPositionForCell(character.gridX, character.gridY);
        character.mesh.position.set(x, y, 0);
    },

    getWorldPositionForCell(gridX, gridY) {
        const worldW = this.gridWidth * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        return {
            x: (gridX * this.cellSize + this.cellSize / 2) - worldW / 2,
            y: (worldH / 2) - (gridY * this.cellSize + this.cellSize / 2)
        };
    },

    getCharacterWorldPos(character) {
        return this.getWorldPositionForCell(character.gridX, character.gridY);
    },

    updateCamera() {
        const focusCharacter = this.getActiveTurnCharacter() || this.getAliveTurnOrder()[0] || this.characters[0];
        if (!focusCharacter) {
            return;
        }

        const worldW = this.gridWidth * this.cellSize;
        const worldH = this.gridHeight * this.cellSize;
        this.camera.position.x = (focusCharacter.gridX * this.cellSize + this.cellSize / 2) - worldW / 2;
        this.camera.position.y = (worldH / 2) - (focusCharacter.gridY * this.cellSize + this.cellSize / 2);
    },

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
    },

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
    },

    faceCharacterToward(attacker, target) {
        const dx = target.gridX - attacker.gridX;
        const dy = target.gridY - attacker.gridY;

        if (Math.abs(dx) >= Math.abs(dy)) {
            this.updateCharacterFacing(attacker, dx >= 0 ? 'right' : 'left');
            return;
        }

        this.updateCharacterFacing(attacker, dy >= 0 ? 'down' : 'up');
    },

    clearAbilityRangeHighlights() {
        while (this.abilityRangeHighlightGroup.children.length > 0) {
            const mesh = this.abilityRangeHighlightGroup.children.pop();
            this.abilityRangeHighlightGroup.remove(mesh);
            mesh.geometry.dispose();
            mesh.material.dispose();
        }
    },

    getAbilityRangeCells(character, ability) {
        if (!character || !ability) {
            return [];
        }

        const range = ability.range ?? 0;
        const includeOrigin = ability.type === 'heal' || ability.type === 'buff';
        const cells = [];

        for (let gridY = character.gridY - range; gridY <= character.gridY + range; gridY++) {
            for (let gridX = character.gridX - range; gridX <= character.gridX + range; gridX++) {
                if (gridX < 0 || gridX >= this.gridWidth || gridY < 0 || gridY >= this.gridHeight) {
                    continue;
                }

                if (this.dungeonMap[gridY][gridX] !== this.TILE_FLOOR) {
                    continue;
                }

                const dx = Math.abs(gridX - character.gridX);
                const dy = Math.abs(gridY - character.gridY);
                if (dx > range || dy > range) {
                    continue;
                }

                if (!includeOrigin && dx === 0 && dy === 0) {
                    continue;
                }

                cells.push({ gridX, gridY });
            }
        }

        return cells;
    },

    updateAbilityRangeHighlights(activeCharacter) {
        if (!activeCharacter || activeCharacter.isDead || activeCharacter.team !== 'player' || this.isGameOver) {
            if (this.abilityRangeHighlightState !== '') {
                this.clearAbilityRangeHighlights();
                this.abilityRangeHighlightState = '';
            }
            return;
        }

        const selectedAbility = activeCharacter.abilities.find((ability) => ability.id === activeCharacter.selectedAbilityId) || null;
        const canAfford = !selectedAbility || selectedAbility.mpCost === 0 || activeCharacter.magicPoints >= selectedAbility.mpCost;
        const canAct = activeCharacter.actionsRemaining >= activeCharacter.attackCost;
        const nextState = [
            activeCharacter.id,
            activeCharacter.gridX,
            activeCharacter.gridY,
            activeCharacter.selectedAbilityId,
            activeCharacter.magicPoints,
            activeCharacter.actionsRemaining,
            canAfford,
            canAct
        ].join('|');

        if (this.abilityRangeHighlightState === nextState) {
            return;
        }

        this.clearAbilityRangeHighlights();
        this.abilityRangeHighlightState = nextState;

        if (!selectedAbility || !canAfford || !canAct) {
            return;
        }

        const cells = this.getAbilityRangeCells(activeCharacter, selectedAbility);
        const highlightColor = new THREE.Color(activeCharacter.accentColor);

        cells.forEach(({ gridX, gridY }) => {
            const geometry = new THREE.PlaneGeometry(this.cellSize - 8, this.cellSize - 8);
            const material = new THREE.MeshBasicMaterial({
                color: highlightColor,
                transparent: true,
                opacity: 0.16,
                depthWrite: false
            });
            const mesh = new THREE.Mesh(geometry, material);
            const { x, y } = this.getWorldPositionForCell(gridX, gridY);
            mesh.position.set(x, y, -1);
            this.abilityRangeHighlightGroup.add(mesh);
        });
    },

    spawnBattleShoutEffect(pos) {
        const size = 48;
        const canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        const cx = size / 2;
        const cy = size / 2;
        const glow = ctx.createRadialGradient(cx, cy, 0, cx, cy, size / 2);
        glow.addColorStop(0.00, 'rgba(255, 230, 80, 1.0)');
        glow.addColorStop(0.25, 'rgba(255, 180, 20, 0.85)');
        glow.addColorStop(0.55, 'rgba(200, 110, 10, 0.40)');
        glow.addColorStop(1.00, 'rgba(140, 60, 0, 0.0)');
        ctx.fillStyle = glow;
        ctx.fillRect(0, 0, size, size);
        const texture = new THREE.CanvasTexture(canvas);

        const geo = new THREE.PlaneGeometry(size, size);
        const mat = new THREE.MeshBasicMaterial({
            map: texture,
            transparent: true,
            depthWrite: false,
            blending: THREE.AdditiveBlending
        });
        const mesh = new THREE.Mesh(geo, mat);
        const startY = pos.y;
        mesh.position.set(pos.x, startY, 10);
        this.scene.add(mesh);

        const durationMs = 600;
        const startTime = performance.now();
        const animate = () => {
            const t = Math.min((performance.now() - startTime) / durationMs, 1);
            mat.opacity = 1 - t;
            mesh.position.y = startY + t * 40;
            if (t < 1) {
                requestAnimationFrame(animate);
            } else {
                this.scene.remove(mesh);
                geo.dispose();
                mat.dispose();
                texture.dispose();
            }
        };
        requestAnimationFrame(animate);
    },

    spawnInflictPainEffect(pos) {
        const size = 52;
        const canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        const cx = size / 2;
        const cy = size / 2;
        const glow = ctx.createRadialGradient(cx, cy, 0, cx, cy, size / 2);
        glow.addColorStop(0.00, 'rgba(255, 235, 255, 1.0)');
        glow.addColorStop(0.18, 'rgba(214, 150, 255, 0.95)');
        glow.addColorStop(0.45, 'rgba(160, 70, 220, 0.55)');
        glow.addColorStop(1.00, 'rgba(60, 15, 90, 0.0)');
        ctx.fillStyle = glow;
        ctx.fillRect(0, 0, size, size);

        const texture = new THREE.CanvasTexture(canvas);
        const geo = new THREE.PlaneGeometry(size, size);
        const mat = new THREE.MeshBasicMaterial({
            map: texture,
            transparent: true,
            depthWrite: false,
            blending: THREE.AdditiveBlending
        });
        const mesh = new THREE.Mesh(geo, mat);
        const startY = pos.y;
        mesh.position.set(pos.x, startY, 10);
        this.scene.add(mesh);

        const durationMs = 520;
        const startTime = performance.now();
        const animate = () => {
            const t = Math.min((performance.now() - startTime) / durationMs, 1);
            const scale = 0.9 + t * 0.5;
            mesh.scale.set(scale, scale, 1);
            mat.opacity = 1 - t;
            mesh.position.y = startY + t * 16;
            if (t < 1) {
                requestAnimationFrame(animate);
            } else {
                this.scene.remove(mesh);
                geo.dispose();
                mat.dispose();
                texture.dispose();
            }
        };
        requestAnimationFrame(animate);
    },

    createHealOrbTexture() {
        const size = 40;
        const canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        const cx = size / 2;
        const cy = size / 2;

        const glow = ctx.createRadialGradient(cx, cy, 0, cx, cy, size / 2);
        glow.addColorStop(0.00, 'rgba(255, 255, 220, 1.0)');
        glow.addColorStop(0.15, 'rgba(200, 255, 160, 1.0)');
        glow.addColorStop(0.35, 'rgba(100, 220, 80, 0.75)');
        glow.addColorStop(0.60, 'rgba(60, 180, 60, 0.35)');
        glow.addColorStop(1.00, 'rgba(20, 120, 40, 0.0)');
        ctx.fillStyle = glow;
        ctx.fillRect(0, 0, size, size);

        const bright = ctx.createRadialGradient(cx, cy, 0, cx, cy, size * 0.12);
        bright.addColorStop(0, 'rgba(255, 255, 255, 1.0)');
        bright.addColorStop(1, 'rgba(255, 255, 255, 0.0)');
        ctx.fillStyle = bright;
        ctx.fillRect(0, 0, size, size);

        return new THREE.CanvasTexture(canvas);
    },

    spawnHealEffect(casterPos, targetPos) {
        const texture = this.createHealOrbTexture();
        const orbSize = 20;
        const durationMs = 420;

        const geo = new THREE.PlaneGeometry(orbSize, orbSize);
        const mat = new THREE.MeshBasicMaterial({
            map: texture,
            transparent: true,
            depthWrite: false,
            blending: THREE.AdditiveBlending
        });
        const mesh = new THREE.Mesh(geo, mat);
        mesh.position.set(casterPos.x, casterPos.y, 10);
        this.scene.add(mesh);

        this.activeProjectiles.push({
            mesh,
            startX: casterPos.x,
            startY: casterPos.y,
            endX: targetPos.x,
            endY: targetPos.y,
            startTime: performance.now(),
            durationMs,
            onImpact: null,
            impactTriggered: false
        });
    },

    createMagicMissileTexture() {
        const size = 48;
        const canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        const cx = size / 2;
        const cy = size / 2;

        const glow = ctx.createRadialGradient(cx, cy, 0, cx, cy, size / 2);
        glow.addColorStop(0.00, 'rgba(255, 255, 255, 1.0)');
        glow.addColorStop(0.12, 'rgba(180, 220, 255, 1.0)');
        glow.addColorStop(0.30, 'rgba(80, 160, 255, 0.85)');
        glow.addColorStop(0.55, 'rgba(40, 80, 255, 0.45)');
        glow.addColorStop(1.00, 'rgba(20, 40, 200, 0.0)');
        ctx.fillStyle = glow;
        ctx.fillRect(0, 0, size, size);

        const bright = ctx.createRadialGradient(cx, cy, 0, cx, cy, size * 0.14);
        bright.addColorStop(0, 'rgba(255, 255, 255, 1.0)');
        bright.addColorStop(1, 'rgba(255, 255, 255, 0.0)');
        ctx.fillStyle = bright;
        ctx.fillRect(0, 0, size, size);

        return new THREE.CanvasTexture(canvas);
    },

    createArrowTexture() {
        const width = 64;
        const height = 16;
        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext('2d');

        const centerY = height / 2;

        ctx.strokeStyle = '#caa26a';
        ctx.lineWidth = 2;
        ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.moveTo(8, centerY);
        ctx.lineTo(width - 14, centerY);
        ctx.stroke();

        ctx.fillStyle = '#f0efe6';
        ctx.beginPath();
        ctx.moveTo(width - 6, centerY);
        ctx.lineTo(width - 14, centerY - 4);
        ctx.lineTo(width - 14, centerY + 4);
        ctx.closePath();
        ctx.fill();

        ctx.strokeStyle = '#f7f7f2';
        ctx.lineWidth = 1.4;
        ctx.beginPath();
        ctx.moveTo(5, centerY);
        ctx.lineTo(1, centerY - 4);
        ctx.moveTo(5, centerY);
        ctx.lineTo(1, centerY + 4);
        ctx.stroke();

        const texture = new THREE.CanvasTexture(canvas);
        texture.magFilter = THREE.NearestFilter;
        texture.minFilter = THREE.NearestFilter;
        return texture;
    },

    spawnArrowProjectile(casterPos, targetPos, onImpact) {
        const texture = this.createArrowTexture();
        const arrowW = 30;
        const arrowH = 8;
        const durationMs = 220;

        const geo = new THREE.PlaneGeometry(arrowW, arrowH);
        const mat = new THREE.MeshBasicMaterial({
            map: texture,
            transparent: true,
            depthWrite: false
        });
        const mesh = new THREE.Mesh(geo, mat);

        const vx = targetPos.x - casterPos.x;
        const vy = targetPos.y - casterPos.y;
        const len = Math.sqrt(vx * vx + vy * vy) || 1;
        const dirX = vx / len;
        const dirY = vy / len;
        const launchOffset = 18;

        const startX = casterPos.x + dirX * launchOffset;
        const startY = casterPos.y + dirY * launchOffset;
        const endX = targetPos.x;
        const endY = targetPos.y;

        mesh.position.set(startX, startY, 9);
        mesh.rotation.z = Math.atan2(vy, vx);
        this.scene.add(mesh);

        this.activeProjectiles.push({
            mesh,
            startX,
            startY,
            endX,
            endY,
            startZ: 9,
            endZ: 9,
            arcHeight: 7,
            startTime: performance.now(),
            durationMs,
            onImpact,
            impactTriggered: false,
            fixedScale: 1,
            fadeStart: 0.88,
            rotationZ: Math.atan2(vy, vx)
        });
    },

    spawnArrowImpactEffect(pos) {
        const size = 34;
        const canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        const ctx = canvas.getContext('2d');
        const cx = size / 2;
        const cy = size / 2;

        const glow = ctx.createRadialGradient(cx, cy, 0, cx, cy, size / 2);
        glow.addColorStop(0.00, 'rgba(255, 255, 235, 1.0)');
        glow.addColorStop(0.22, 'rgba(255, 220, 150, 0.95)');
        glow.addColorStop(0.55, 'rgba(255, 160, 80, 0.45)');
        glow.addColorStop(1.00, 'rgba(80, 40, 10, 0.0)');
        ctx.fillStyle = glow;
        ctx.fillRect(0, 0, size, size);

        const texture = new THREE.CanvasTexture(canvas);
        const geo = new THREE.PlaneGeometry(size, size);
        const mat = new THREE.MeshBasicMaterial({
            map: texture,
            transparent: true,
            depthWrite: false,
            blending: THREE.AdditiveBlending
        });
        const mesh = new THREE.Mesh(geo, mat);
        mesh.position.set(pos.x, pos.y, 10);
        this.scene.add(mesh);

        const durationMs = 140;
        const startTime = performance.now();
        const animate = () => {
            const t = Math.min((performance.now() - startTime) / durationMs, 1);
            const scale = 0.55 + t * 0.9;
            mesh.scale.set(scale, scale, 1);
            mat.opacity = 1 - t;
            if (t < 1) {
                requestAnimationFrame(animate);
            } else {
                this.scene.remove(mesh);
                geo.dispose();
                mat.dispose();
                texture.dispose();
            }
        };
        requestAnimationFrame(animate);
    },

    spawnMagicMissileProjectiles(casterPos, targetPos, onImpact) {
        const texture = this.createMagicMissileTexture();
        const missileSize = 22;
        const durationMs = 300;
        const staggerMs = 90;

        const vx = targetPos.x - casterPos.x;
        const vy = targetPos.y - casterPos.y;
        const len = Math.sqrt(vx * vx + vy * vy) || 1;
        const perpX = (-vy / len) * 9;
        const perpY = (vx / len) * 9;

        for (let i = 0; i < 2; i++) {
            const sign = i === 0 ? 1 : -1;
            const offsetX = perpX * sign;
            const offsetY = perpY * sign;

            const geo = new THREE.PlaneGeometry(missileSize, missileSize);
            const mat = new THREE.MeshBasicMaterial({
                map: texture,
                transparent: true,
                depthWrite: false,
                blending: THREE.AdditiveBlending
            });
            const mesh = new THREE.Mesh(geo, mat);
            mesh.position.set(casterPos.x + offsetX, casterPos.y + offsetY, 10);
            this.scene.add(mesh);

            const isLast = i === 1;
            this.activeProjectiles.push({
                mesh,
                startX: casterPos.x + offsetX,
                startY: casterPos.y + offsetY,
                endX: targetPos.x + offsetX * 0.2,
                endY: targetPos.y + offsetY * 0.2,
                startTime: performance.now() + i * staggerMs,
                durationMs,
                onImpact: isLast ? onImpact : null,
                impactTriggered: false
            });
        }
    },

    updateProjectiles(nowMs) {
        this.activeProjectiles = this.activeProjectiles.filter((proj) => {
            const elapsed = nowMs - proj.startTime;
            if (elapsed < 0) {
                proj.mesh.visible = false;
                return true;
            }
            proj.mesh.visible = true;
            const t = Math.min(1, elapsed / proj.durationMs);

            proj.mesh.position.x = proj.startX + (proj.endX - proj.startX) * t;
            proj.mesh.position.y = proj.startY + (proj.endY - proj.startY) * t;

            if (typeof proj.startZ === 'number' || typeof proj.endZ === 'number') {
                const startZ = typeof proj.startZ === 'number' ? proj.startZ : proj.mesh.position.z;
                const endZ = typeof proj.endZ === 'number' ? proj.endZ : startZ;
                const arc = proj.arcHeight ? Math.sin(t * Math.PI) * proj.arcHeight : 0;
                proj.mesh.position.z = startZ + (endZ - startZ) * t + arc;
            }

            if (typeof proj.rotationZ === 'number') {
                proj.mesh.rotation.z = proj.rotationZ;
            }

            if (typeof proj.fixedScale === 'number') {
                proj.mesh.scale.set(proj.fixedScale, proj.fixedScale, 1);
            } else {
                const scale = t < 0.75 ? 1.0 + t * 0.3 : 1.3 - ((t - 0.75) / 0.25) * 1.0;
                proj.mesh.scale.set(Math.max(0.01, scale), Math.max(0.01, scale), 1);
            }

            const fadeStart = typeof proj.fadeStart === 'number' ? proj.fadeStart : 0.80;
            if (proj.mesh.material && typeof proj.mesh.material.opacity === 'number') {
                proj.mesh.material.opacity = t < fadeStart ? 1.0 : 1.0 - ((t - fadeStart) / Math.max(0.01, 1 - fadeStart));
            }

            if (t >= 1) {
                if (proj.onImpact && !proj.impactTriggered) {
                    proj.impactTriggered = true;
                    proj.onImpact();
                }
                this.scene.remove(proj.mesh);
                if (proj.mesh.geometry && proj.mesh.geometry.dispose) {
                    proj.mesh.geometry.dispose();
                }
                if (proj.mesh.material && proj.mesh.material.map && proj.mesh.material.map.dispose) {
                    proj.mesh.material.map.dispose();
                }
                if (proj.mesh.material && proj.mesh.material.dispose) {
                    proj.mesh.material.dispose();
                }
                return false;
            }
            return true;
        });
    },

    playHitAnimation(character) {
        if (!character || !character.mesh || character.removedFromScene) {
            return;
        }

        character.hitAnimEndTime = performance.now() + 1000;
    },

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

        const envelope = 1 - progress;
        const pulse = 1 + (Math.sin(progress * Math.PI * 10) * 0.18 * envelope);
        character.mesh.scale.set(pulse, pulse, 1);

        const flashAmount = Math.abs(Math.sin(progress * Math.PI * 12)) * 0.65 * envelope;
        const baseColor = new THREE.Color(character.baseColorHex);
        const flashTarget = character.baseColorHex === 0xffffff
            ? new THREE.Color(0xff4444)
            : new THREE.Color(0xffffff);
        const hitColor = baseColor.clone().lerp(flashTarget, flashAmount);
        character.mesh.material.color.copy(hitColor);
    },

    fadeAndRemoveCharacter(character) {
        if (!character.isDead || !character.mesh || character.removedFromScene) {
            return;
        }

        const fadeDurationFrames = 60;
        character.fadeFrames += 1;

        const opacity = Math.max(0, 1 - (character.fadeFrames / fadeDurationFrames));
        character.mesh.material.transparent = true;
        character.mesh.material.opacity = opacity;

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
};
