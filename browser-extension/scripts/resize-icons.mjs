/**
 * Resizes the default icon PNG to 16, 32, 48, 96 for Chrome extension action icons,
 * and 300x300 for the store listing (extension logo).
 * Run: node scripts/resize-icons.mjs
 */
import sharp from "sharp";
import { mkdir } from "fs/promises";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "..");
const srcPath = join(root, "public", "icons", "icon-default.png");
const outDir = join(root, "public", "icons");
const storeAssetsDir = join(root, "store-assets");
const sizes = [16, 32, 48, 96];

await mkdir(outDir, { recursive: true });
await mkdir(storeAssetsDir, { recursive: true });

for (const size of sizes) {
  const outPath = join(outDir, `icon-default-${size}.png`);
  await sharp(srcPath)
    .resize(size, size)
    .png()
    .toFile(outPath);
  console.log(`Wrote ${outPath}`);
}

const storeLogoPath = join(storeAssetsDir, "extension-logo-300x300.png");
await sharp(srcPath)
  .resize(300, 300)
  .png()
  .toFile(storeLogoPath);
console.log(`Wrote ${storeLogoPath} (store listing logo)`);

console.log("Done.");
