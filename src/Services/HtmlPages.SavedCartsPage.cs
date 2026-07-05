namespace KrogerShopperMcp.Services;

internal static partial class HtmlPages
{
    public static string RenderSavedCartsPage(string username)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Saved Carts</title>
          {{FaviconLinks}}
          {{GoogleFonts}}
          <style>
            {{SharedAppStyles}}

            .layout {
              display: grid;
              grid-template-columns: minmax(280px, 360px) minmax(0, 1fr);
              min-height: 72vh;
            }

            .sidebar {
              border-right: 1px solid var(--line);
              background: rgba(255,255,255,.42);
              padding: 18px;
            }

            .cart-list {
              display: grid;
              gap: 10px;
              margin-top: 14px;
            }

            .cart-button {
              width: 100%;
              text-align: left;
              border: 1px solid var(--line);
              border-radius: 10px;
              padding: 16px;
              background: rgba(255,255,255,.78);
              cursor: pointer;
              transition: transform 120ms ease, border-color 120ms ease, background 120ms ease;
            }

            .cart-button:hover,
            .cart-button.is-active {
              transform: translateY(-1px);
              border-color: rgba(28,42,143,.28);
              background: rgba(57,80,212,.08);
            }

            .cart-name {
              display: block;
              font-weight: 700;
              font-size: 1rem;
              margin-bottom: 6px;
            }

            .detail {
              padding: 24px;
              display: grid;
              gap: 18px;
              align-content: start;
            }

            .detail-head {
              display: flex;
              justify-content: space-between;
              gap: 16px;
              align-items: end;
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
              gap: 10px;
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

            .price-row {
              display: flex;
              gap: 12px;
              flex-wrap: wrap;
              align-items: center;
              font-size: .95rem;
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

            .summary-action {
              padding: 8px 10px;
              border-radius: 10px;
            }

            .item-link {
              color: var(--accent);
              font-weight: 700;
              text-decoration: none;
            }

            .control-row {
              display: flex;
              gap: 10px;
              flex-wrap: wrap;
              align-items: end;
            }

            .quantity-input {
              width: 96px;
              padding: 10px 12px;
              border-radius: 10px;
              border: 1px solid var(--line);
              font: inherit;
            }

            .danger-button {
              display: inline-flex;
              align-items: center;
              justify-content: center;
              padding: 12px 16px;
              border-radius: 10px;
              border: 0;
              cursor: pointer;
              font: inherit;
              font-weight: 700;
              background: #8f1c34;
              color: #fff;
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

            @media (max-width: 920px) {
              .layout { grid-template-columns: 1fr; }
              .sidebar { border-right: 0; border-bottom: 1px solid var(--line); }
            }

            @media (max-width: 640px) {
              .shell { width: calc(100% - 16px); margin: 8px auto; }
              .topbar, .detail, .sidebar { padding: 16px; }
              .detail-head { align-items: start; flex-direction: column; }
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
                <h1>Saved Carts</h1>
                <p class="meta">Signed in as <strong>{{username}}</strong></p>
              </div>
              <div class="actions">
                <a class="ghost-link" href="/">Home</a>
                <a class="ghost-link" href="/current-cart">Current Cart</a>
                <a class="ghost-link" href="/sent-history">Sent History</a>
              </div>
            </div>

            <div class="layout">
              <aside class="sidebar">
                <h2>Cart Library</h2>
                <p class="meta">Pick a saved cart to inspect what is actually in it.</p>
                <div id="cart-list" class="cart-list">
                  <div class="loading">Loading saved carts...</div>
                </div>
              </aside>

              <section class="detail">
                <div id="status" class="status"></div>
                <details class="expander">
                  <summary>Add Item</summary>
                  <div class="expander-body">
                    <p class="empty-copy">Paste a Kroger product URL or raw UPC to add it to the selected saved cart.</p>
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
                  <summary>Manage Saved Cart</summary>
                  <div class="expander-body">
                    <p class="empty-copy">Rename, duplicate, or delete the selected saved cart.</p>
                    <div class="add-form-row">
                      <input id="rename-cart-name" class="text-input" type="text" placeholder="Rename selected cart">
                      <button id="rename-cart-button" class="button" type="button">Rename</button>
                    </div>
                    <div class="add-form-row">
                      <input id="duplicate-cart-name" class="text-input" type="text" placeholder="Duplicate to new cart name">
                      <button id="duplicate-cart-button" class="button" type="button">Duplicate</button>
                      <button id="delete-cart-button" class="danger-button" type="button">Delete</button>
                    </div>
                  </div>
                </details>
                <div id="detail-panel" class="empty-state">
                  <h2>No Cart Selected</h2>
                  <p class="empty-copy">Choose a cart on the left to see product images, quantity, price, and Kroger links.</p>
                </div>
              </section>
            </div>
          </div>

          <script>
            const CACHE_TTL_MS = 15 * 60 * 1000;
            const SAVED_CARTS_CACHE_KEY = "kroger-saved-carts-view-v1";
            const money = new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" });
            const cartList = document.getElementById("cart-list");
            const detailPanel = document.getElementById("detail-panel");
            const statusBox = document.getElementById("status");
            const addItemIdentifierInput = document.getElementById("add-item-identifier");
            const addItemQuantityInput = document.getElementById("add-item-quantity");
            const addItemButton = document.getElementById("add-item-button");
            const renameCartNameInput = document.getElementById("rename-cart-name");
            const renameCartButton = document.getElementById("rename-cart-button");
            const duplicateCartNameInput = document.getElementById("duplicate-cart-name");
            const duplicateCartButton = document.getElementById("duplicate-cart-button");
            const deleteCartButton = document.getElementById("delete-cart-button");
            let carts = [];
            let activeCartName = null;

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

            function formatDate(value) {
              if (!value) return "";
              const date = new Date(value);
              return date.toLocaleString();
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

            function updateSavedCartCache(mutator) {
              const payload = readCache(SAVED_CARTS_CACHE_KEY);
              if (!payload || !Array.isArray(payload.carts)) {
                return false;
              }

              const updatedPayload = mutator(payload);
              if (!updatedPayload) {
                return false;
              }

              writeCache(SAVED_CARTS_CACHE_KEY, updatedPayload);
              return true;
            }

            function recalculateSavedCartTotals(cart) {
              const items = Array.isArray(cart.items) ? cart.items : [];
              cart.item_count = items.length;
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

            function patchSavedCartItem(cartName, upc, quantity) {
              const updated = updateSavedCartCache((payload) => {
                const cart = payload.carts.find((entry) => entry.name === cartName);
                if (!cart || !Array.isArray(cart.items)) {
                  return null;
                }

                const index = cart.items.findIndex((item) => item.upc === upc);
                if (index < 0) {
                  return null;
                }

                if (quantity <= 0) {
                  cart.items.splice(index, 1);
                }
                else
                {
                  const item = cart.items[index];
                  item.quantity = quantity;
                  item.total_price = typeof item.unit_price === "number" ? item.unit_price * quantity : null;
                }

                cart.updated_at_utc = new Date().toISOString();
                recalculateSavedCartTotals(cart);
                return payload;
              });

              if (!updated) {
                return false;
              }

              const payload = readCache(SAVED_CARTS_CACHE_KEY);
              carts = Array.isArray(payload?.carts) ? payload.carts : carts;
              return true;
            }

            function renderCartList() {
              if (!carts.length) {
                cartList.innerHTML = `
                  <div class="empty-state">
                    <h3>No saved carts yet</h3>
                    <p class="empty-copy">Save a tracked cart or staged cart first, then this viewer becomes useful.</p>
                  </div>
                `;
                detailPanel.innerHTML = `
                  <h2>Nothing Saved Yet</h2>
                  <p class="empty-copy">There are no saved carts to browse right now.</p>
                `;
                return;
              }

              cartList.innerHTML = carts.map((cart) => `
                <button class="cart-button ${cart.name === activeCartName ? "is-active" : ""}" data-cart-name="${escapeHtml(cart.name)}">
                  <span class="cart-name">${escapeHtml(cart.name)}</span>
                  <div class="mini">${cart.item_count} items · ${cart.total_quantity} total qty</div>
                  <div class="mini">${cart.total_price == null ? "Price unavailable" : formatMoney(cart.total_price)}</div>
                </button>
              `).join("");

              cartList.querySelectorAll(".cart-button").forEach((button) => {
                button.addEventListener("click", () => {
                  activeCartName = button.getAttribute("data-cart-name");
                  renderCartList();
                  renderDetail();
                });
              });
            }

            function renderDetail() {
              const cart = carts.find((entry) => entry.name === activeCartName) ?? carts[0];
              if (!cart) {
                return;
              }

              activeCartName = cart.name;
              renameCartNameInput.value = cart.name;
              if (!duplicateCartNameInput.value || duplicateCartNameInput.dataset.lastSource === cart.name) {
                duplicateCartNameInput.value = `${cart.name} Copy`;
              }
              duplicateCartNameInput.dataset.lastSource = cart.name;

              const itemsHtml = cart.items.length
                ? cart.items.map((item) => {
                    const image = item.image_url
                      ? `<img src="${escapeHtml(item.image_url)}" alt="${escapeHtml(item.description || item.upc)}">`
                      : `<div class="image-fallback">No Image</div>`;
                    const itemLink = item.product_url
                      ? `<a class="item-link" href="${escapeHtml(item.product_url)}" target="_blank" rel="noreferrer">Open on Kroger</a>`
                      : `<span class="mini">No Kroger page link</span>`;
                    return `
                      <article class="item-card" data-cart-name="${escapeHtml(cart.name)}" data-upc="${escapeHtml(item.upc)}">
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
                  }).join("")
                : `
                  <div class="empty-state">
                    <h3>Empty Cart</h3>
                    <p class="empty-copy">This saved cart does not currently contain any valid items.</p>
                  </div>
                `;

              detailPanel.innerHTML = `
                <div class="detail-head">
                  <div>
                    <p class="eyebrow">Saved Cart</p>
                    <h2>${escapeHtml(cart.name)}</h2>
                    <p class="meta">Updated ${escapeHtml(formatDate(cart.updated_at_utc))}</p>
                  </div>
                  <div class="meta">
                    ${cart.total_price == null ? "Price unavailable" : `Estimated total ${formatMoney(cart.total_price)}`}
                  </div>
                </div>
                <div class="item-grid">${itemsHtml}</div>
              `;

              detailPanel.querySelectorAll("[data-action='save']").forEach((button) => {
                button.addEventListener("click", async () => {
                  const card = button.closest("[data-cart-name][data-upc]");
                  const cartName = card?.getAttribute("data-cart-name");
                  const upc = card?.getAttribute("data-upc");
                  const quantityInput = card?.querySelector("[data-role='quantity']");
                  const quantity = Number(quantityInput?.value ?? 0);
                  await updateSavedCartQuantity(cartName, upc, quantity);
                });
              });

              detailPanel.querySelectorAll("[data-action='remove']").forEach((button) => {
                button.addEventListener("click", async () => {
                  const card = button.closest("[data-cart-name][data-upc]");
                  const cartName = card?.getAttribute("data-cart-name");
                  const upc = card?.getAttribute("data-upc");
                  await removeSavedCartItem(cartName, upc);
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

            async function loadSavedCarts(forceRefresh = false) {
              try {
                let payload = null;
                if (!forceRefresh) {
                  payload = readCache(SAVED_CARTS_CACHE_KEY);
                }

                if (!payload) {
                  const response = await fetch("/api/saved-carts-view", { credentials: "same-origin" });
                  payload = await handleApiResponse(response);
                  if (!payload) {
                    return;
                  }

                  writeCache(SAVED_CARTS_CACHE_KEY, payload);
                }

                carts = Array.isArray(payload.carts) ? payload.carts : [];
                if (!carts.some((cart) => cart.name === activeCartName)) {
                  activeCartName = carts[0]?.name ?? null;
                }
                renderCartList();
                renderDetail();
              } catch (error) {
                cartList.innerHTML = `
                  <div class="empty-state">
                    <h3>Couldn’t Load Saved Carts</h3>
                    <p class="empty-copy">${escapeHtml(error.message)}</p>
                  </div>
                `;
                detailPanel.innerHTML = `
                  <h2>Load Failed</h2>
                  <p class="empty-copy">The saved cart browser could not load its data right now.</p>
                `;
              }
            }

            async function updateSavedCartQuantity(cartName, upc, quantity) {
              if (!cartName || !upc || !Number.isFinite(quantity) || quantity < 0) {
                setStatus("Quantity must be zero or greater.");
                return;
              }

              setStatus("Updating saved cart...");
              const response = await fetch("/api/saved-carts-set-quantity", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ name: cartName, upc, quantity })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              setStatus(quantity === 0 ? "Item removed from saved cart." : "Saved cart quantity updated.");
              if (patchSavedCartItem(cartName, upc, quantity)) {
                renderCartList();
                renderDetail();
                return;
              }

              await loadSavedCarts(true);
            }

            async function removeSavedCartItem(cartName, upc) {
              if (!cartName || !upc) {
                return;
              }

              setStatus("Removing item from saved cart...");
              const response = await fetch("/api/saved-carts-remove-item", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ name: cartName, upc })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              setStatus("Item removed from saved cart.");
              if (patchSavedCartItem(cartName, upc, 0)) {
                renderCartList();
                renderDetail();
                return;
              }

              await loadSavedCarts(true);
            }

            async function addSavedCartItem() {
              const cartName = activeCartName ?? carts[0]?.name;
              const identifier = addItemIdentifierInput.value.trim();
              const quantity = Number(addItemQuantityInput.value ?? 1);

              if (!cartName) {
                setStatus("Pick a saved cart first.");
                return;
              }

              if (!identifier) {
                setStatus("Paste a Kroger URL or UPC first.");
                return;
              }

              if (!Number.isFinite(quantity) || quantity <= 0) {
                setStatus("Quantity must be at least 1.");
                return;
              }

              setStatus("Adding item to saved cart...");
              const response = await fetch("/api/saved-carts-add-item", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ name: cartName, identifier, quantity })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              addItemIdentifierInput.value = "";
              addItemQuantityInput.value = "1";
              setStatus("Item added to saved cart.");
              await loadSavedCarts(true);
            }

            async function renameSavedCart() {
              const cartName = activeCartName ?? carts[0]?.name;
              const newName = renameCartNameInput.value.trim();
              if (!cartName) {
                setStatus("Pick a saved cart first.");
                return;
              }

              if (!newName) {
                setStatus("Enter a new cart name first.");
                return;
              }

              setStatus("Renaming saved cart...");
              const response = await fetch("/api/saved-carts-rename", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ name: cartName, newName })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              activeCartName = payload.name ?? newName;
              setStatus("Saved cart renamed.");
              await loadSavedCarts(true);
            }

            async function duplicateSavedCart() {
              const cartName = activeCartName ?? carts[0]?.name;
              const newName = duplicateCartNameInput.value.trim();
              if (!cartName) {
                setStatus("Pick a saved cart first.");
                return;
              }

              if (!newName) {
                setStatus("Enter a new cart name first.");
                return;
              }

              setStatus("Duplicating saved cart...");
              const response = await fetch("/api/saved-carts-duplicate", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ name: cartName, newName })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              activeCartName = payload.name ?? newName;
              setStatus("Saved cart duplicated.");
              await loadSavedCarts(true);
            }

            async function deleteSavedCart() {
              const cartName = activeCartName ?? carts[0]?.name;
              if (!cartName) {
                setStatus("Pick a saved cart first.");
                return;
              }

              setStatus("Deleting saved cart...");
              const response = await fetch("/api/saved-carts-delete", {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ name: cartName })
              });

              const payload = await handleApiResponse(response);
              if (!payload) {
                return;
              }

              activeCartName = null;
              renameCartNameInput.value = "";
              duplicateCartNameInput.value = "";
              setStatus("Saved cart deleted.");
              await loadSavedCarts(true);
            }

            addItemButton.addEventListener("click", () => {
              addSavedCartItem().catch((error) => setStatus(error.message));
            });
            renameCartButton.addEventListener("click", () => {
              renameSavedCart().catch((error) => setStatus(error.message));
            });
            duplicateCartButton.addEventListener("click", () => {
              duplicateSavedCart().catch((error) => setStatus(error.message));
            });
            deleteCartButton.addEventListener("click", () => {
              deleteSavedCart().catch((error) => setStatus(error.message));
            });

            loadSavedCarts();
          </script>
        </body>
        </html>
        """;
    }
}
