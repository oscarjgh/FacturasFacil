const fs = require("fs");

let md = fs.readFileSync("design_markup.html", "utf8");

// 1) Resolve sc-if blocks. Default featuredPlan = "Contador".
//    isFeatContador = true → keep inner; the other two → drop.
const trueConds = new Set(["isFeatContador"]);
md = md.replace(
  /<sc-if value="\{\{ (\w+) \}\}"[^>]*>([\s\S]*?)<\/sc-if>/g,
  (_, cond, inner) => (trueConds.has(cond) ? inner : "")
);

// 2) Substitute remaining template variables.
md = md.replace(/\{\{\s*accent\s*\}\}/g, "#1f6feb");
md = md.replace(/\{\{\s*year\s*\}\}/g, "2026");

// 2b) Re-route CTA links to the real app (this landing IS the public root now).
//     The footer link whose text is the bare URL -> /app.html (same tab, informational).
//     Every other CTA -> /app.html?signup=1 so it opens the registro form directly.
const APP_URL = "https://facturasfacil-production.up.railway.app";
md = md.replace(
  new RegExp(`<a href="${APP_URL.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")}"([^>]*)>([\\s\\S]*?)<\\/a>`, "g"),
  (m, attrs, inner) => {
    const isFooterUrl = inner.trim() === "facturasfacil-production.up.railway.app";
    const href = isFooterUrl ? "/app.html" : "/app.html?signup=1";
    // drop target/rel so navigation stays in the same tab within our own site
    const cleaned = attrs.replace(/\s+target="[^"]*"/g, "").replace(/\s+rel="[^"]*"/g, "");
    return `<a href="${href}"${cleaned}>${inner}</a>`;
  }
);

// 3) Convert style-hover="..." into real :hover CSS via generated classes.
const hoverRules = [];
let hi = 0;
md = md.replace(/\sstyle-hover="([^"]*)"/g, (_, css) => {
  const cls = "hv" + hi++;
  hoverRules.push(`.${cls}:hover{${css}}`);
  // We append the class to the element. Find we are inside a tag; just emit a marker
  // attribute that we post-process below.
  return ` data-hv="${cls}"`;
});
// Move data-hv into a real class attribute (merge if class exists, else add).
md = md.replace(/<([a-zA-Z0-9]+)([^>]*?)data-hv="([^"]+)"([^>]*)>/g, (m, tag, pre, cls, post) => {
  const all = pre + post;
  if (/\sclass="/.test(all)) {
    return `<${tag}${(pre + post).replace(/\sclass="([^"]*)"/, ` class="$1 ${cls}"`)}>`;
  }
  return `<${tag}${pre} class="${cls}"${post}>`;
});

const css = `
@import url('https://fonts.googleapis.com/css2?family=Manrope:wght@400;600;700;800&display=swap');
* { margin:0; padding:0; box-sizing:border-box; }
html { scroll-behavior:smooth; }
body { font-family:'Manrope', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; -webkit-font-smoothing:antialiased; }
a { text-decoration:none; color:inherit; }
${hoverRules.join("\n")}
`.trim();

const html = `<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>FacturasFacil — De ZIP caótico a Excel ordenado en segundos</title>
  <meta name="description" content="Procesa tus facturas CFDI (3.3 y 4.0) automáticamente. Sube tu ZIP o RAR y descarga un Excel ordenado por fecha en segundos. Ideal para contadores y despachos en México.">
  <link rel="icon" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Crect width='100' height='100' rx='20' fill='%231f6feb'/%3E%3Ctext x='50' y='70' font-size='56' text-anchor='middle' fill='%23fff'%3E%E2%9A%A1%3C/text%3E%3C/svg%3E">
  <style>
${css}
  </style>
</head>
<body>
${md.trim()}
</body>
</html>`;

fs.writeFileSync("../src/FacturasFacil.Api/wwwroot/index.html", html);
console.log("Generado: wwwroot/index.html");
console.log("Tamaño:", (html.length / 1024).toFixed(1), "KB");
console.log("Reglas hover:", hoverRules.length);
// sanity: any leftover template syntax?
const left = [...html.matchAll(/\{\{[^}]*\}\}|<sc-if|style-hover|data-hv/g)];
console.log("Restos de plantilla:", left.length === 0 ? "ninguno ✓" : left.map(x=>x[0]));
