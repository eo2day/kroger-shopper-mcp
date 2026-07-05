namespace KrogerShopperMcp.Services;

internal static partial class HtmlPages
{
    public static string RenderCurrentCartPage(string username)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Current Cart</title>
          {{FaviconLinks}}
          {{GoogleFonts}}
          <style>
            {{SharedAppStyles}}
            .shell { width: min(1200px, calc(100% - 32px)); }
            h1 { font-size: clamp(2rem, 4vw, 3.2rem); }
            h2 { font-size: 1.35rem; }

            .summary {
              display: flex;
              justify-content: space-between;
              gap: 16px;
              flex-wrap: wrap;
              padding: 18px;
              border: 1px solid var(--line);
              border-radius: 10px;
              background: rgba(255,255,255,.55);
            }

            .summary-metrics {
              display: flex;
              gap: 12px;
              flex-wrap: wrap;
            }

            .cart-action-row {
              display: flex;
              gap: 10px;
              flex-wrap: wrap;
            }

            .pill {
              display: inline-flex;
              align-items: center;
              gap: 6px;
              padding: 8px 10px;
              border-radius: 10px;
              background: rgba(57,80,212,.08);
              color: var(--accent);
              font-weight: 700;
            }

            .item-grid {
              display: grid;
              gap: 12px;
            }

            .item-card {
              display: grid;
              grid-template-columns: 112px minmax(0, 1fr);
              gap: 16px;
              padding: 14px;
              border: 1px solid var(--line);
              border-radius: 10px;
              background: var(--panel);
              backdrop-filter: blur(12px);
            }

            .item-image {
              width: 112px;
              height: 112px;
              border-radius: 10px;
              background: linear-gradient(180deg, rgba(57,80,212,.14), rgba(57,80,212,.04));
              border: 1px solid rgba(57,80,212,.08);
              display: grid;
              place-items: center;
              overflow: hidden;
            }

            .item-image img {
              width: 100%;
              height: 100%;
              object-fit: contain;
              background: #fff;
            }

            .image-fallback {
              font-size: .8rem;
              font-weight: 700;
              color: var(--muted);
              text-transform: uppercase;
              letter-spacing: .06em;
            }

            .item-meta {
              display: grid;
              gap: 12px;
              min-width: 0;
            }

            .item-title {
              font-weight: 700;
              font-size: 1.05rem;
            }

            .mini {
              font-size: .92rem;
              color: var(--muted);
            }

            .price-row, .control-row {
              display: flex;
              gap: 12px;
              flex-wrap: wrap;
              align-items: center;
            }

            .item-link {
              color: var(--accent);
              font-weight: 700;
              text-decoration: none;
            }

            .quantity-input {
              width: 96px;
              padding: 10px 12px;
              border-radius: 10px;
              border: 1px solid var(--line);
              font: inherit;
            }

            .empty-state {
              border: 1px dashed rgba(31,42,31,.18);
              border-radius: 10px;
              padding: 24px;
              background: rgba(255,255,255,.45);
            }

            .loading, .status {
              color: var(--muted);
              font-weight: 700;
            }

            .add-form {
              display: grid;
              gap: 10px;
              padding: 14px;
              border: 1px solid var(--line);
              border-radius: 10px;
              background: rgba(255,255,255,.5);
            }

            .expander {
              border: 1px solid var(--line);
              border-radius: 10px;
              background: rgba(255,255,255,.5);
              overflow: hidden;
            }

            .expander > summary {
              list-style: none;
              cursor: pointer;
              padding: 14px;
              font-weight: 700;
              display: flex;
              align-items: center;
              justify-content: space-between;
              gap: 12px;
            }

            .expander > summary::-webkit-details-marker {
              display: none;
            }

            .expander > summary::after {
              content: "+";
              color: var(--accent);
              font-size: 1.2rem;
              line-height: 1;
            }

            .expander[open] > summary::after {
              content: "−";
            }

            .expander-body {
              display: grid;
              gap: 10px;
              padding: 0 14px 14px;
            }

            .add-form-row {
              display: flex;
              gap: 10px;
              flex-wrap: wrap;
              align-items: end;
            }

            .text-input {
              min-width: min(420px, 100%);
              flex: 1 1 320px;
              padding: 10px 12px;
              border-radius: 10px;
              border: 1px solid var(--line);
              font: inherit;
            }

            @media (max-width: 720px) {
              .shell { width: calc(100% - 16px); margin: 8px auto; }
              .topbar, .content { padding: 16px; }
              .item-card { grid-template-columns: 1fr; }
              .item-image { width: 100%; height: 180px; }
            }
          </style>
        </head>
        <body>
          <div class="shell">
            <div class="topbar">
              <div>
                <p class="eyebrow">Kroger Assistant</p>
                <h1>Working Cart</h1>
                <p class="meta">Signed in as <strong>{{username}}</strong></p>
              </div>
              <div class="actions">
                <a class="ghost-link" href="/">Home</a>
                <a class="ghost-link" href="/saved-carts">Saved Carts</a>
                <a class="ghost-link" href="/sent-history">Sent History</a>
              </div>
            </div>

            <div class="content">
              <div id="summary" class="summary">
                <div>
                  <h2>Working Cart</h2>
                  <p class="meta">This is the local staged cart. Review it here, then send the whole batch to Kroger when ready.</p>
                </div>
                <div class="summary-metrics">
                  <span class="pill">Loading...</span>
                </div>
              </div>

              <div id="status" class="status"></div>

              <div class="cart-action-row">
                <button id="send-to-kroger-button" class="button" type="button">Send to Kroger</button>
                <button id="clear-cart-button" class="danger-button" type="button">Clear Cart</button>
              </div>

              <details class="expander">
                <summary>Add Item</summary>
                <div class="expander-body">
                  <p class="meta">Paste a Kroger product URL or raw UPC to add it to the live tracked cart.</p>
                  <div class="add-form-row">
                    <input id="add-item-identifier" class="text-input" type="text" placeholder="Kroger URL or UPC">
                    <label>
                      <span class="mini">Qty</span>
                      <input id="add-item-quantity" class="quantity-input" type="number" min="1" step="1" value="1">
                    </label>
                    <button id="add-item-button" class="button" type="button">Add Item</button>
                  </div>
                </div>
              </details>

              <details class="expander">
                <summary>Save As Saved Cart</summary>
                <div class="expander-body">
                  <p class="meta">Save the current working cart as a named saved cart.</p>
                  <div class="add-form-row">
                    <input id="save-cart-name" class="text-input" type="text" placeholder="Saved cart name">
                    <button id="save-cart-button" class="button" type="button">Save Current Cart</button>
                  </div>
                </div>
              </details>

              <div id="item-grid" class="item-grid">
                <div class="loading">Loading working cart...</div>
              </div>
            </div>
          </div>

          <script>
            const CACHE_TTL_MS = 15 * 60 * 1000;
            const CURRENT_CART_CACHE_KEY = "kroger-current-cart-view-v1";
            const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
            const itemGrid = document.getElementById("item-grid");
            const summary = document.getElementById("summary");
            const statusBox = document.getElementById("status");
            const addItemIdentifierInput = document.getElementById("add-item-identifier");
            const addItemQuantityInput = document.getElementById("add-item-quantity");
            const addItemButton = document.getElementById("add-item-button");
            const sendToKrogerButton = document.getElementById("send-to-kroger-button");
            const clearCartButton = document.getElementById("clear-cart-button");
            const saveCartNameInput = document.getElementById("save-cart-name");
            const saveCartButton = document.getElementById("save-cart-button");
            let currentCart = null;

            function escapeHtml(value) {
              return String(value ?? "")
                .replaceAll("&", "&amp;")
                .replaceAll("<", "&lt;")
                .replaceAll(">", "&gt;")
                .replaceAll('"', "&quot;")
                .replaceAll("'", "&#39;");
            }

            function formatMoney(value) {
              return typeof value === "number" ? money.format(value) : "Unavailable";
            }

            function setStatus(message) {
              statusBox.textContent = message ?? "";
            }

            function readCache(key) {
              try {
                const raw = localStorage.getItem(key);
                if (!raw) {
                  return null;
                }

                const parsed = JSON.parse(raw);
                if (!parsed || typeof parsed !== "object") {
                  return null;
                }

                if (typeof parsed.savedAt !== "number" || Date.now() - parsed.savedAt > CACHE_TTL_MS) {
                  localStorage.removeItem(key);
                  return null;
                }

                return parsed.payload ?? null;
              } catch {
                return null;
              }
            }

            function writeCache(key, payload) {
              try {
                localStorage.setItem(key, JSON.stringify({
                  savedAt: Date.now(),
                  payload
                }));
              } catch {
              }
            }

            function updateCurrentCartCache(mutator) {
              const payload = readCache(CURRENT_CART_CACHE_KEY);
              if (!payload || !Array.isArray(payload.items)) {
                return false;
              }

              const updatedPayload = mutator(payload);
              if (!updatedPayload) {
                return false;
              }

              writeCache(CURRENT_CART_CACHE_KEY, updatedPayload);
              return true;
            }

            function recalculateCurrentCartTotals(cart) {
              const items = Array.isArray(cart.items) ? cart.items : [];
              cart.count = items.length;
              cart.total_quantity = items.reduce((sum, item) => sum + (Number(item.quantity) || 0), 0);

              let totalPrice = 0;
              let hasPrice = items.length > 0;
              for (const item of items) {
                const itemTotal = typeof item.total_price === "number"
                  ? item.total_price
                  : (typeof item.unit_price === "number" ? item.unit_price * (Number(item.quantity) || 0) : null);

                if (itemTotal == null || Number.isNaN(itemTotal)) {
                  hasPrice = false;
                  continue;
                }

                totalPrice += itemTotal;
              }

              cart.total_price = items.length === 0 ? null : (hasPrice ? totalPrice : null);
            }

            function patchCurrentCartItem(upc, quantity) {
              const updated = updateCurrentCartCache((payload) => {
                const index = payload.items.findIndex((item) => item.upc === upc);
                if (index < 0) {
                  return null;
                }

                if (quantity <= 0) {
                  payload.items.splice(index, 1);
                }
                else
                {
                  const item = payload.items[index];
                  item.quantity = quantity;
                  item.total_price = typeof item.unit_price === "number" ? item.unit_price * quantity : null;
                }

                recalculateCurrentCartTotals(payload);
                return payload;
              });

              if (!updated) {
                return false;
              }

              currentCart = readCache(CURRENT_CART_CACHE_KEY) ?? currentCart;
              return true;
            }

            function renderSummary() {
              if (!currentCart) {
                return;
              }

              summary.innerHTML = `
                <div>
                  <h2>Working Cart</h2>
                  <p class="meta">This is the local staged cart. Review it here, then send the whole batch to Kroger when ready.</p>
                </div>
                <div class="summary-metrics">
                  <span class="pill">${currentCart.count} items</span>
                  <span class="pill">${currentCart.total_quantity} total qty</span>
                  <span class="pill">${currentCart.total_price == null ? "Price unavailable" : `Estimated ${formatMoney(currentCart.total_price)}`}</span>
                </div>
              `;
            }

            function renderItems() {
              if (!currentCart || !Array.isArray(currentCart.items) || !currentCart.items.length) {
                itemGrid.innerHTML = `
                  <div class="empty-state">
                    <h3>Working cart is empty</h3>
                    <p class="empty-copy">Add items here or load a saved cart first, then send the whole batch to Kroger.</p>
                  </div>
                `;
                return;
              }

              itemGrid.innerHTML = currentCart.items.map((item) => {
                const image = item.image_url
                  ? `<img src="${escapeHtml(item.image_url)}" alt="${escapeHtml(item.description || item.upc)}">`
                  : `<div class="image-fallback">No Image</div>`;
                const itemLink = item.product_url
                  ? `<a class="item-link" href="${escapeHtml(item.product_url)}" target="_blank" rel="noreferrer">Open on Kroger</a>`
                  : `<span class="mini">No Kroger page link</span>`;

                return `
                  <article class="item-card" data-upc="${escapeHtml(item.upc)}">
                    <div class="item-image">${image}</div>
                    <div class="item-meta">
                      <div>
                        <div class="item-title">${escapeHtml(item.description || item.upc)}</div>
                        <div class="mini">${escapeHtml(item.brand || "")}${item.size ? ` · ${escapeHtml(item.size)}` : ""}</div>
                        <div class="mini">UPC ${escapeHtml(item.upc)}</div>
                      </div>
                      <div class="price-row">
                        <span class="pill">Qty ${item.quantity}</span>
                        <span>Each ${formatMoney(item.unit_price)}</span>
                        <span>Total ${formatMoney(item.total_price)}</span>
                      </div>
                      <div class="price-note">
                        ${item.promo_price != null && item.regular_price != null && item.promo_price !== item.regular_price
                          ? `Promo ${formatMoney(item.promo_price)} · Regular ${formatMoney(item.regular_price)}`
                          : ""}
                      </div>
                      <div class="stock-note">${item.stock_level ? `Stock ${escapeHtml(item.stock_level)}` : "Stock unavailable"}</div>
                      <div>${itemLink}</div>
                      <div class="control-row">
                        <label>
                          <span class="mini">Quantity</span>
                          <input class="quantity-input" type="number" min="0" step="1" value="${item.quantity}" data-role="quantity">
                        </label>
                        <button class="button" type="button" data-action="save">Update Qty</button>
                        <button class="danger-button" type="button" data-action="remove">Remove</button>
                      </div>
                    </div>
                  </article>
                `;
              }).join("");

              itemGrid.querySelectorAll("[data-action='save']").forEach((button) => {
                button.addEventListener("click", async () => {
                  const card = button.closest("[data-upc]");
                  const upc = card?.getAttribute("data-upc");
                  const quantityInput = card?.querySelector("[data-role='quantity']");
                  const quantity = Number(quantityInput?.value ?? 0);
                  await updateQuantity(upc, quantity);
                });
              });

              itemGrid.querySelectorAll("[data-action='remove']").forEach((button) => {
                button.addEventListener("click", async () => {
                  const card = button.closest("[data-upc]");
                  const upc = card?.getAttribute("data-upc");
                  await removeItem(upc);
                });
              });
            }

            async function handleApiResponse(response) {
              if (response.status === 401) {
                window.location.href = "/login";
                return null;
              }

              if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
              }

              return response.json();
            }

            async function loadCurrentCart(forceRefresh = false) {
              let payload = null;
              if (!forceRefresh) {
                payload = readCache(CURRENT_CART_CACHE_KEY);
              }

              if (!payload) {
                const response = await fetch("/api/current-cart-view", { credentials: "same-origin" });
                payload = await handleApiResponse(response);
                if (!payload) {
                  return;
                }

                writeCache(CURRENT_CART_CACHE_KEY, payload);
              }

              currentCart = payload;
              renderSummary();
              renderItems();
            }

            async function updateQuantity(upc, quantity) {
              if (!upc || !Number.isFinite(quantity) || quantity < 0) {
                setStatus("Quantity must be zero or greater.");
                return;
              }

              setStatus("Updating quantity...");
              const response = await fetch("/api/current-cart-set-quantity", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ upc, quantity })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              setStatus(quantity === 0 ? "Item removed." : "Quantity updated.");
              if (patchCurrentCartItem(upc, quantity)) {
                renderSummary();
                renderItems();
                return;
              }

              await loadCurrentCart(true);
            }

            async function removeItem(upc) {
              if (!upc) {
                return;
              }

              setStatus("Removing item...");
              const response = await fetch("/api/current-cart-remove-item", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ upc })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              setStatus("Item removed.");
              if (patchCurrentCartItem(upc, 0)) {
                renderSummary();
                renderItems();
                return;
              }

              await loadCurrentCart(true);
            }

            async function addCurrentCartItem() {
              const identifier = addItemIdentifierInput.value.trim();
              const quantity = Number(addItemQuantityInput.value ?? 1);

              if (!identifier) {
                setStatus("Paste a Kroger URL or UPC first.");
                return;
              }

              if (!Number.isFinite(quantity) || quantity <= 0) {
                setStatus("Quantity must be at least 1.");
                return;
              }

              setStatus("Adding item to working cart...");
              const response = await fetch("/api/current-cart-add-item", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ identifier, quantity })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              addItemIdentifierInput.value = "";
              addItemQuantityInput.value = "1";
              setStatus(payload.blocked ? "Item could not be added." : "Item added to working cart.");
              await loadCurrentCart(true);
            }

            async function clearCurrentCart() {
              if (!currentCart || !Array.isArray(currentCart.items) || currentCart.items.length === 0) {
                setStatus("Working cart is already empty.");
                return;
              }

              setStatus("Clearing working cart...");
              const response = await fetch("/api/clear-staged-cart", {
                method: "POST",
                credentials: "same-origin"
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              updateCurrentCartCache((cached) => {
                cached.items = [];
                recalculateCurrentCartTotals(cached);
                return cached;
              });

              currentCart = readCache(CURRENT_CART_CACHE_KEY) ?? {
                items: [],
                count: 0,
                total_quantity: 0,
                total_price: null
              };

              setStatus("Working cart cleared.");
              renderSummary();
              renderItems();
            }

            async function sendCurrentCartToKroger() {
              if (!currentCart || !Array.isArray(currentCart.items) || currentCart.items.length === 0) {
                setStatus("Working cart is empty.");
                return;
              }

              setStatus("Sending working cart to Kroger...");
              const response = await fetch("/api/send-tracked-cart", {
                method: "POST",
                credentials: "same-origin"
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              const successful = Number(payload.successful ?? 0);
              const count = Number(payload.count ?? 0);
              if (successful === count) {
                updateCurrentCartCache((cached) => {
                  cached.items = [];
                  recalculateCurrentCartTotals(cached);
                  return cached;
                });

                currentCart = readCache(CURRENT_CART_CACHE_KEY) ?? {
                  items: [],
                  count: 0,
                  total_quantity: 0,
                  total_price: null
                };

                renderSummary();
                renderItems();
              }

              setStatus(`Sent ${successful} of ${count} working cart items to Kroger.${payload.batch_id ? ` Batch ${payload.batch_id}.` : ""}`);
            }

            async function saveCurrentCart() {
              const name = saveCartNameInput.value.trim();
              if (!name) {
                setStatus("Enter a saved cart name first.");
                return;
              }

              setStatus("Saving working cart...");
              const response = await fetch("/api/save-cart", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ name })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              setStatus("Working cart saved.");
            }

            addItemButton.addEventListener("click", () => {
              addCurrentCartItem().catch((error) => setStatus(error.message));
            });
            sendToKrogerButton.addEventListener("click", () => {
              sendCurrentCartToKroger().catch((error) => setStatus(error.message));
            });
            clearCartButton.addEventListener("click", () => {
              clearCurrentCart().catch((error) => setStatus(error.message));
            });
            saveCartButton.addEventListener("click", () => {
              saveCurrentCart().catch((error) => setStatus(error.message));
            });

            loadCurrentCart().catch((error) => {
              itemGrid.innerHTML = `
                <div class="empty-state">
                  <h3>Couldn’t Load Working Cart</h3>
                  <p class="empty-copy">${escapeHtml(error.message)}</p>
                </div>
              `;
            });
          </script>
        </body>
        </html>
        """;
    }
}
