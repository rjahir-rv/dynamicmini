const fs = require("fs");
const path = require("path");
const sharp = require("sharp");

const projectRoot = path.resolve(__dirname, "..");
const sourcePath = path.join(projectRoot, "src", "DynamicMiniPlayer", "Assets", "DynamicMiniPlayer.svg");
const outputPath = path.join(projectRoot, "src", "DynamicMiniPlayer", "Assets", "DynamicMiniPlayer.ico");
const previewPath = path.join(projectRoot, "src", "DynamicMiniPlayer", "Assets", "DynamicMiniPlayer.png");
const sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];

async function buildIcon() {
    const images = await Promise.all(sizes.map(size =>
        sharp(sourcePath).resize(size, size).png().toBuffer()));

    const header = Buffer.alloc(6);
    header.writeUInt16LE(0, 0);
    header.writeUInt16LE(1, 2);
    header.writeUInt16LE(images.length, 4);

    const directory = Buffer.alloc(images.length * 16);
    let offset = header.length + directory.length;
    images.forEach((image, index) => {
        const entry = index * 16;
        const size = sizes[index];
        directory.writeUInt8(size === 256 ? 0 : size, entry);
        directory.writeUInt8(size === 256 ? 0 : size, entry + 1);
        directory.writeUInt8(0, entry + 2);
        directory.writeUInt8(0, entry + 3);
        directory.writeUInt16LE(1, entry + 4);
        directory.writeUInt16LE(32, entry + 6);
        directory.writeUInt32LE(image.length, entry + 8);
        directory.writeUInt32LE(offset, entry + 12);
        offset += image.length;
    });

    fs.writeFileSync(outputPath, Buffer.concat([header, directory, ...images]));
    fs.writeFileSync(previewPath, images[images.length - 1]);
    console.log(outputPath);
}

buildIcon().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
