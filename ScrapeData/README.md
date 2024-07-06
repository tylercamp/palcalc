A scraper for paldb.cc which collects all pals, traits, and pal icons required for Pal Calc.

Requires Node.js v19+. Run with:

```js
npm install
node fetch.js
```

Paldb.cc is behind Cloudflare and includes rate-limiting, mainly for the CDN which serves pal icons, so you may need to run this multiple times to fetch all of the latest pal icons. (Occasional errors when scraping icons is expected.) The script will only fetch data which hasn't been scraped yet, and the latest raw icons used by Pal Calc are stored in `out/raw-icons` with `git`, so there shouldn't be many new icons to fetch if there's a game update.

Results are written to `out`, where `scraped-pals.json` and `scraped-traits.json` should be copied to `PalCalc.GenDB/ref`. Run the `PalCalc.GenDB` project to update the `db.json` file used internally.

The icons under `out/icons` should be copied to `PalCalc.UI/Resources/Pals`. Note that this folder has `Human.png`, which isn't scraped by this tool, and is used as a placeholder for pals without an icon. This icon should not be deleted.