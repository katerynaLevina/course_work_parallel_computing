"use strict";

// Routes and stage names mirror src/SearchContracts/ApiRoutes.cs.
const ROUTES = { ws: "/ws/search", health: "/api/health", stats: "/api/stats", index: "/api/index", doc: "/api/doc/" };
const STAGE = { expansion: "query_expansion", keyword: "keyword_search", done: "done", error: "error" };
const MIN_TERM_LENGTH = 2;

const $ = (id) => document.getElementById(id);
const input = $("search-input");
const resultsList = $("results");

let socket = null;
let queued = null;      // message waiting for the socket to open
let inFlight = 0;       // searches sent but not finished; only the last one is rendered
let lastQuery = "";
let retried = false;
let results = [];
let terms = [];
let selected = -1;
let previewId = null;

// ---------- WebSocket ----------

function send(message) {
  if (socket && socket.readyState === WebSocket.OPEN) return socket.send(message);
  queued = message;
  if (socket) return; // still connecting; onopen sends the queued message
  const scheme = location.protocol === "https:" ? "wss:" : "ws:";
  socket = new WebSocket(scheme + "//" + location.host + ROUTES.ws);
  socket.onopen = () => { socket.send(queued); queued = null; };
  socket.onmessage = (e) => onStage(JSON.parse(e.data));
  socket.onclose = onSocketClose;
}

function onSocketClose() {
  socket = null;
  if (inFlight === 0) return;
  inFlight = 0;
  if (retried) return showMessage("Connection to the server was lost.", true);
  retried = true;
  search(lastQuery); // reconnect once and replay the interrupted search
}

function search(query) {
  lastQuery = query;
  inFlight++;
  resetPipeline();
  send(JSON.stringify({ query }));
}

function onStage({ stage, data }) {
  const latest = inFlight === 1;
  if (stage === STAGE.done || stage === STAGE.error) inFlight = Math.max(0, inFlight - 1);
  if (!latest) return;

  if (stage === STAGE.expansion) {
    terms = data.expanded_terms;
    renderTerms(terms, data.error);
    setPill("pill-expand", "done");
    setPill("pill-search", "active");
  } else if (stage === STAGE.keyword) {
    $("search-time").textContent = data.time_ms + " ms";
    setPill("pill-search", "done");
    renderResults(data.results);
  } else if (stage === STAGE.done) {
    setPill("pill-done", "done");
    retried = false;
  } else if (stage === STAGE.error) {
    $("pipeline").hidden = true;
    showMessage(data.message, true);
  }
}

// ---------- Pipeline pills ----------

function setPill(id, state) {
  $(id).dataset.state = state;
}

function resetPipeline() {
  $("pipeline").hidden = false;
  setPill("pill-expand", "active");
  setPill("pill-search", "pending");
  setPill("pill-done", "pending");
  $("search-time").textContent = "";
  $("terms").replaceChildren();
  showMessage("");
}

function renderTerms(list, error) {
  const chips = list.map((term) => textElement("span", "chip", term));
  if (error) {
    const note = textElement("span", "chip muted", "Expansion unavailable");
    note.title = error;
    chips.push(note);
  }
  $("terms").replaceChildren(...chips);
}

function showMessage(text, isError = false) {
  const el = $("message");
  el.textContent = text;
  el.classList.toggle("error", isError);
  el.hidden = !text;
}

// ---------- Results ----------

const DOC_ICON = '<svg viewBox="0 0 26 30" aria-hidden="true"><path d="M4 1h12l7 7v19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V3a2 2 0 0 1 2-2z"/><path d="M16 1v7h7M7 15h12M7 19h12M7 23h8"/></svg>';

function renderResults(list) {
  results = list;
  closePreview();
  resultsList.replaceChildren(...list.map(resultRow));
  $("content").hidden = list.length === 0;
  if (list.length === 0) showMessage("No documents match “" + lastQuery + "”.");
  select(list.length > 0 ? 0 : -1);
}

function resultRow(result, index) {
  const row = document.createElement("li");
  row.className = "result";
  row.setAttribute("role", "option");
  row.insertAdjacentHTML("afterbegin", DOC_ICON); // static markup, no server text
  row.append(
    textElement("span", "result-title", result.title || result.doc_id),
    textElement("span", "result-score", result.score.toFixed(2)),
    highlight(textElement("span", "result-snippet", ""), result.snippet));
  row.addEventListener("click", () => { select(index); openPreview(); });
  return row;
}

function textElement(tag, className, text) {
  const el = document.createElement(tag);
  el.className = className;
  el.textContent = text;
  return el;
}

/** Appends text to el, wrapping query and expanded terms in <mark>; text nodes only. */
function highlight(el, text) {
  const words = (lastQuery + " " + terms.join(" ")).toLowerCase().split(/[^\p{L}\p{N}]+/u)
    .filter((w) => w.length >= MIN_TERM_LENGTH)
    .map((w) => w.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"));
  if (words.length === 0) {
    el.append(text);
    return el;
  }
  text.split(new RegExp("(" + words.join("|") + ")", "giu")).forEach((part, i) => {
    if (i % 2 === 0) return el.append(part);
    const mark = document.createElement("mark");
    mark.textContent = part;
    el.append(mark);
  });
  return el;
}

function select(index) {
  selected = index;
  [...resultsList.children].forEach((row, i) => row.setAttribute("aria-selected", String(i === index)));
  const row = resultsList.children[index];
  if (!row) return;
  row.scrollIntoView({ block: "nearest" });
  if (previewId !== null) openPreview();
}

// ---------- Preview ----------

async function openPreview() {
  const result = results[selected];
  if (!result) return;
  previewId = result.doc_id;
  $("panel").classList.add("split");
  $("preview").hidden = false;
  $("preview-title").textContent = result.title || result.doc_id;
  $("preview-id").textContent = result.doc_id;
  $("preview-text").textContent = "Loading…";

  const path = result.doc_id.split("/").map(encodeURIComponent).join("/");
  try {
    const response = await fetch(ROUTES.doc + path);
    if (!response.ok) throw new Error("Document not found.");
    const doc = await response.json();
    if (previewId !== doc.doc_id) return; // selection moved on meanwhile
    $("preview-title").textContent = doc.title || doc.doc_id;
    $("preview-text").textContent = "";
    highlight($("preview-text"), doc.text);
  } catch (e) {
    if (previewId === result.doc_id) $("preview-text").textContent = e.message;
  }
}

function closePreview() {
  previewId = null;
  $("panel").classList.remove("split");
  $("preview").hidden = true;
}

function clearAll() {
  input.value = "";
  results = [];
  closePreview();
  resultsList.replaceChildren();
  $("content").hidden = true;
  $("pipeline").hidden = true;
  showMessage("");
}

// ---------- Keyboard ----------

document.addEventListener("keydown", (e) => {
  if ((e.key === "k" && (e.metaKey || e.ctrlKey)) || (e.key === "/" && !isField(e.target))) {
    e.preventDefault();
    input.focus();
    input.select();
  } else if (e.key === "Escape") {
    e.preventDefault(); // keep the search input from clearing itself natively
    if (!$("index-popover").hidden) return togglePopover(false);
    previewId !== null ? closePreview() : clearAll();
  } else if ((e.key === "ArrowDown" || e.key === "ArrowUp") && results.length > 0) {
    e.preventDefault();
    const step = e.key === "ArrowDown" ? 1 : -1;
    select(Math.min(results.length - 1, Math.max(0, selected + step)));
  }
});

function isField(el) {
  return el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement;
}

$("search-form").addEventListener("submit", (e) => {
  e.preventDefault();
  const query = input.value.trim();
  if (!query || (query === lastQuery && inFlight > 0)) return;
  if (query === lastQuery && selected >= 0 && previewId === null) return openPreview();
  retried = false;
  search(query);
});

// ---------- Status chip + index popover ----------

async function loadStatus() {
  const [stats, health] = await Promise.all([getJson(ROUTES.stats), getJson(ROUTES.health)]);
  $("doc-count").textContent = stats ? stats.documents.toLocaleString() + " documents" : "Index unavailable";
  $("ollama-dot").classList.toggle("on", Boolean(health && health.ollama));
}

async function getJson(url) {
  try {
    const response = await fetch(url);
    return response.ok ? await response.json() : null;
  } catch {
    return null;
  }
}

function togglePopover(open) {
  $("index-popover").hidden = !open;
  $("status-chip").setAttribute("aria-expanded", String(open));
}

async function rebuild() {
  const button = $("rebuild");
  const output = $("rebuild-result");
  button.disabled = true;
  output.textContent = "Building…";
  try {
    const response = await fetch(ROUTES.index, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ threads: Number($("threads").value) }),
    });
    const body = await response.json();
    output.textContent = response.ok
      ? body.documents_indexed.toLocaleString() + " documents in " + body.time_ms + " ms"
      : body.error || "Rebuild failed.";
  } catch {
    output.textContent = "Rebuild failed.";
  }
  button.disabled = false;
  loadStatus();
}

$("status-chip").addEventListener("click", () => togglePopover($("index-popover").hidden));
$("threads").addEventListener("input", (e) => { $("threads-value").textContent = e.target.value; });
$("rebuild").addEventListener("click", rebuild);
document.addEventListener("click", (e) => {
  if (!e.target.closest(".footer")) togglePopover(false);
});

loadStatus();
