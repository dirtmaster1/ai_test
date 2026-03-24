const scene = new THREE.Scene();
scene.background = new THREE.Color(0x0a0a0a);
scene.fog = new THREE.FogExp2(0x0a0a0a, 0.008);

const camera = new THREE.PerspectiveCamera(75, window.innerWidth / window.innerHeight, 0.1, 2000);
camera.position.set(0, 2, 0);

const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setSize(window.innerWidth, window.innerHeight);
renderer.shadowMap.enabled = true;
document.body.appendChild(renderer.domElement);

// Lighting
const ambientLight = new THREE.AmbientLight(0x1a1a2e, 0.3);
scene.add(ambientLight);

const directionalLight = new THREE.DirectionalLight(0x4a4a6a, 0.8);
directionalLight.position.set(0, 8, -100);
directionalLight.castShadow = true;
directionalLight.shadow.camera.near = 0.1;
directionalLight.shadow.camera.far = 500;
directionalLight.shadow.camera.left = -15;
directionalLight.shadow.camera.right = 15;
directionalLight.shadow.camera.top = 15;
directionalLight.shadow.camera.bottom = -15;
scene.add(directionalLight);

// Corridor dimensions
const corridorWidth = 6;
const corridorHeight = 5;
const corridorLength = 400;

// Floor
const floorGeo = new THREE.PlaneGeometry(corridorWidth, corridorLength);
const floorMat = new THREE.MeshPhongMaterial({ color: 0x1a1a1a, side: THREE.DoubleSide });
const floor = new THREE.Mesh(floorGeo, floorMat);
floor.rotation.x = -Math.PI / 2;
floor.position.y = 0;
floor.position.z = -corridorLength / 2;
floor.receiveShadow = true;
scene.add(floor);

// Ceiling
const ceilingGeo = new THREE.PlaneGeometry(corridorWidth, corridorLength);
const ceilingMat = new THREE.MeshPhongMaterial({ color: 0x0a0a0a, side: THREE.DoubleSide });
const ceiling = new THREE.Mesh(ceilingGeo, ceilingMat);
ceiling.rotation.x = Math.PI / 2;
ceiling.position.y = corridorHeight;
ceiling.position.z = -corridorLength / 2;
scene.add(ceiling);

// Left Wall
const leftWallGeo = new THREE.PlaneGeometry(corridorLength, corridorHeight);
const wallMat = new THREE.MeshPhongMaterial({ color: 0x2a2a2a, side: THREE.DoubleSide });
const leftWall = new THREE.Mesh(leftWallGeo, wallMat);
leftWall.position.x = -corridorWidth / 2;
leftWall.position.y = corridorHeight / 2;
leftWall.position.z = -corridorLength / 2;
leftWall.rotation.y = Math.PI / 2;
leftWall.receiveShadow = true;
scene.add(leftWall);

// Right Wall
const rightWall = new THREE.Mesh(leftWallGeo, wallMat);
rightWall.position.x = corridorWidth / 2;
rightWall.position.y = corridorHeight / 2;
rightWall.position.z = -corridorLength / 2;
rightWall.rotation.y = Math.PI / 2;
rightWall.receiveShadow = true;
scene.add(rightWall);

// Stone pillars along the corridor
for (let i = 0; i < 20; i++) {
    const pillarGeo = new THREE.CylinderGeometry(0.4, 0.5, corridorHeight, 8);
    const pillarMat = new THREE.MeshPhongMaterial({ color: 0x3a3a3a });
    const pillar = new THREE.Mesh(pillarGeo, pillarMat);
    pillar.position.x = i % 2 === 0 ? -2 : 2;
    pillar.position.y = corridorHeight / 2;
    pillar.position.z = -i * 20;
    pillar.castShadow = true;
    pillar.receiveShadow = true;
    scene.add(pillar);
}

// Eerie purple/blue corridor lights
for (let i = 0; i < 30; i++) {
    const light = new THREE.PointLight(0x5a3a8a, 0.8, 25);
    light.position.set(i % 2 === 0 ? -2.5 : 2.5, 3.5, -i * 13 - 20);
    scene.add(light);
}

// Add a distant eerie glow at the end of corridor
const glowGeometry = new THREE.SphereGeometry(30, 32, 32);
const glowMaterial = new THREE.MeshBasicMaterial({ color: 0x4a3a6a, transparent: true, opacity: 0.1 });
const distantGlow = new THREE.Mesh(glowGeometry, glowMaterial);
distantGlow.position.z = -corridorLength / 2;
scene.add(distantGlow);

const clock = new THREE.Clock();

function animate() {
    requestAnimationFrame(animate);
    
    const t = clock.getElapsedTime();
    
    // Subtle camera movement down the corridor
    camera.position.x = Math.sin(t * 0.2) * 0.5;
    camera.position.z = -t * 10;
    camera.lookAt(0, 2, camera.position.z - 50);
    
    // Loop camera back to start when it reaches the end
    if (camera.position.z < -corridorLength + 50) {
        clock.start();
    }
    
    renderer.render(scene, camera);
}

animate();

// Create Dark Dungeon title overlay
const titleOverlay = document.createElement('div');
titleOverlay.style.position = 'fixed';
titleOverlay.style.top = '50%';
titleOverlay.style.left = '50%';
titleOverlay.style.transform = 'translate(-50%, -50%)';
titleOverlay.style.fontFamily = '"Creepster", "Gripen", "Permanent Marker", serif, sans-serif';
titleOverlay.style.color = '#8b0000';
titleOverlay.style.textShadow = '0 0 30px rgba(90, 58, 138, 0.6), 0 0 60px rgba(90, 58, 138, 0.3)';
titleOverlay.style.fontWeight = 'bold';
titleOverlay.style.letterSpacing = '0.2em';
titleOverlay.style.textTransform = 'uppercase';
titleOverlay.style.opacity = '0.7';
titleOverlay.style.textAlign = 'center';
titleOverlay.style.pointerEvents = 'auto';
titleOverlay.style.cursor = 'pointer';
titleOverlay.style.zIndex = '10';

const mainTitle = document.createElement('div');
mainTitle.textContent = 'Dark Dungeon';
mainTitle.style.fontSize = '5vw';
mainTitle.style.marginBottom = '1.5rem';
titleOverlay.appendChild(mainTitle);

const subtitle = document.createElement('div');
subtitle.textContent = 'click to enter';
subtitle.style.fontSize = '2vw';
subtitle.style.opacity = '0.8';
subtitle.style.letterSpacing = '0.1em';
titleOverlay.appendChild(subtitle);

// Add link to gothic font if not already loaded
if (!document.querySelector('link[href*="Creepster"]')) {
    const fontLink = document.createElement('link');
    fontLink.href = 'https://fonts.googleapis.com/css2?family=Creepster&family=Gripen&family=Permanent+Marker&display=swap';
    fontLink.rel = 'stylesheet';
    document.head.appendChild(fontLink);
}

document.body.appendChild(titleOverlay);

// Handle title overlay click - switch to blue cube scene
titleOverlay.addEventListener('click', () => {
    titleOverlay.remove();
    
    // Clear the current corridor scene
    scene.clear();
    
    // Create blue cube
    const cubeGeo = new THREE.BoxGeometry(2, 2, 2);
    const cubeMat = new THREE.MeshPhongMaterial({ color: 0x0066ff });
    const cube = new THREE.Mesh(cubeGeo, cubeMat);
    cube.castShadow = true;
    cube.receiveShadow = true;
    scene.add(cube);
    
    // Add light for the cube
    const cubeLight = new THREE.DirectionalLight(0xffffff, 1);
    cubeLight.position.set(5, 10, 5);
    cubeLight.castShadow = true;
    scene.add(cubeLight);
    
    const cubeAmbient = new THREE.AmbientLight(0xffffff, 0.5);
    scene.add(cubeAmbient);
    
    // Update camera to view cube
    camera.position.set(0, 0, 5);
    camera.lookAt(0, 0, 0);
    
    // Rotate cube animation
    function animateCube() {
        requestAnimationFrame(animateCube);
        cube.rotation.x += 0.01;
        cube.rotation.y += 0.01;
        renderer.render(scene, camera);
    }
    
    animateCube();
});

// Handle window resize
window.addEventListener('resize', () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
});
