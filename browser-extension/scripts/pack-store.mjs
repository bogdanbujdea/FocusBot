/**
 * Zips the contents of dist/ for Edge Add-ons and Chrome Web Store submission.
 * Run from browser-extension: npm run pack
 * Output: focusbot-extension.zip (manifest and all files at zip root)
 */
import archiver from "archiver";
import { createWriteStream } from "fs";
import { existsSync } from "fs";
import { join, dirname } from "path";
import { fileURLToPath } from "url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, "..");
const distDir = join(root, "dist");
const outPath = join(root, "focusbot-extension.zip");

if (!existsSync(distDir)) {
  console.error("dist/ not found. Run npm run build first.");
  process.exit(1);
}

const output = createWriteStream(outPath);
const archive = archiver("zip", { zlib: { level: 9 } });

const done = new Promise((resolve, reject) => {
  output.on("close", () => {
    console.log(`Created ${outPath} (${archive.pointer()} bytes)`);
    resolve();
  });
  archive.on("error", reject);
  output.on("error", reject);
});

archive.pipe(output);
archive.directory(distDir, false);
archive.finalize();
await done;
