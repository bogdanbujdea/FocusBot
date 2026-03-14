/**
 * Resizes the default icon PNG to 16, 32, 48, 96 for Chrome extension action icons.
 * Run: node scripts/resize-icons.mjs
 */
import sharp from "sharp";
import { readdir, mkdir } from "fs/promises";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "..");
const srcPath = join(root, "public", "icons", "icon-default.png");
const outDir = join(root, "public", "icons");
const sizes = [16, 32, 48, 96];

await mkdir(outDir, { recursive: true });

for (const size of sizes) {
  const outPath = join(outDir, `icon-default-${size}.png`);
  await sharp(srcPath)
    .resize(size, size)
    .png()
    .toFile(outPath);
  console.log(`Wrote ${outPath}`);
}

console.log("Done.");
