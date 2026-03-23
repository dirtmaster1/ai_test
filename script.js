// Simple Three.js Game: Move the cube to the target sphere

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x87CEEB); // Sky blue background

const camera = new THREE.PerspectiveCamera(75, window.innerWidth / window.innerHeight, 0.1, 1000);
camera.position.set(0, 5, 10);
camera.lookAt(0, 0, 0);

const renderer = new THREE.WebGLRenderer();
renderer.setSize(window.innerWidth, window.innerHeight);
document.getElementById('game').appendChild(renderer.domElement);

// Ground plane
const planeGeometry = new THREE.PlaneGeometry(20, 20);
const planeMaterial = new THREE.MeshBasicMaterial({ color: 0x228B22, side: THREE.DoubleSide });
const plane = new THREE.Mesh(planeGeometry, planeMaterial);
plane.rotation.x = -Math.PI / 2;
scene.add(plane);

// Player cube
const cubeGeometry = new THREE.BoxGeometry(1, 1, 1);
const cubeMaterial = new THREE.MeshBasicMaterial({ color: 0xff0000 });
const cube = new THREE.Mesh(cubeGeometry, cubeMaterial);
cube.position.set(0, 0.5, 0);
scene.add(cube);

// Target sphere
const sphereGeometry = new THREE.SphereGeometry(0.5, 32, 32);
const sphereMaterial = new THREE.MeshBasicMaterial({ color: 0x0000ff });
const sphere = new THREE.Mesh(sphereGeometry, sphereMaterial);
sphere.position.set(Math.random() * 10 - 5, 0.5, Math.random() * 10 - 5);
scene.add(sphere);

// Keyboard controls
const keys = {};
document.addEventListener('keydown', (event) => {
    keys[event.code] = true;
});
document.addEventListener('keyup', (event) => {
    keys[event.code] = false;
});

// Check collision
function checkCollision() {
    const distance = cube.position.distanceTo(sphere.position);
    if (distance < 1) {
        // Move sphere to new random position
        sphere.position.set(Math.random() * 10 - 5, 0.5, Math.random() * 10 - 5);
        // Maybe add score or something, but keep simple
    }
}

function animate() {
    requestAnimationFrame(animate);

    // Move cube based on keys
    const speed = 0.1;
    if (keys['ArrowUp'] || keys['KeyW']) cube.position.z -= speed;
    if (keys['ArrowDown'] || keys['KeyS']) cube.position.z += speed;
    if (keys['ArrowLeft'] || keys['KeyA']) cube.position.x -= speed;
    if (keys['ArrowRight'] || keys['KeyD']) cube.position.x += speed;

    // Keep cube on plane
    cube.position.x = Math.max(-9, Math.min(9, cube.position.x));
    cube.position.z = Math.max(-9, Math.min(9, cube.position.z));

    checkCollision();

    renderer.render(scene, camera);
}

animate();

// Handle window resize
window.addEventListener('resize', () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
});