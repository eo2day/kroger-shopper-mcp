namespace KrogerShopperMcp.Services;

internal static partial class HtmlPages
{
    public static string RenderSentHistoryPage(string username)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>Sent to Kroger History</title>
          {{FaviconLinks}}
          {{GoogleFonts}}
          <style>
            {{SharedAppStyles}}
            .shell { width: min(1200px, calc(100% - 32px)); }
            h1 { font-size: clamp(2rem, 4vw, 3.2rem); }

            .day-group {
              margin-bottom: 24px;
            }

            .day-header {
              display: flex;
              align-items: center;
              gap: 10px;
              padding: 10px 14px;
              border-radius: 10px;
              background: rgba(57,80,212,.08);
              color: var(--accent);
              font-weight: 700;
              font-size: 1.05rem;
              margin-bottom: 12px;
            }

            .day-header .count {
              margin-left: auto;
              font-size: .85rem;
              opacity: .8;
              font-weight: 600;
            }

            .history-list {
              display: grid;
              gap: 10px;
            }

            .history-card {
              display: grid;
              grid-template-columns: minmax(0, 1fr) auto;
              gap: 12px;
              padding: 12px 14px;
              border: 1px solid var(--line);
              border-radius: 10px;
              background: var(--panel);
              backdrop-filter: blur(12px);
            }

            .history-meta {
              display: flex;
              flex-direction: column;
              gap: 4px;
            }

            .history-upc {
              font-family: var(--font-mono);
              font-size: .85rem;
              color: var(--muted);
            }

            .history-time {
              font-size: .8rem;
              color: var(--muted);
            }

            .history-source {
              font-size: .8rem;
              color: var(--accent);
              font-weight: 600;
            }

            .history-qty {
              display: inline-flex;
              align-items: center;
              gap: 6px;
              padding: 6px 10px;
              border-radius: 8px;
              background: rgba(57,80,212,.08);
              color: var(--accent);
              font-weight: 700;
              font-size: .9rem;
              white-space: nowrap;
            }

            .empty-state {
              text-align: center;
              padding: 40px 20px;
              color: var(--muted);
            }

            .loading {
              text-align: center;
              padding: 40px 20px;
              color: var(--muted);
            }
          </style>
        </head>
        <body>
          <div class="shell">
            <div class="topbar">
              <div>
                <p class="eyebrow">Kroger Assistant</p>
                <h1>Sent to Kroger History</h1>
                <p class="meta">Signed in as <strong>{{username}}</strong></p>
              </div>
              <div class="actions">
                <a class="ghost-link" href="/saved-carts">Saved Carts</a>
                <a class="ghost-link" href="/current-cart">Current Cart</a>
                <a class="ghost-link" href="/">Home</a>
              </div>
            </div>
            <div class="content" id="history-content">
              <div class="loading">Loading history…</div>
            </div>
          </div>

          <script>
            (async () => {
              const container = document.getElementById('history-content');
              try {
                const res = await fetch('/api/sent-to-kroger-history?limit=500');
                const data = await res.json();
                if (!data.ok) {
                  container.innerHTML = '<div class="empty-state">Failed to load history.</div>';
                  return;
                }
                render(data.items || []);
              } catch (e) {
                container.innerHTML = '<div class="empty-state">Failed to load history.</div>';
              }

              function render(items) {
                if (!items.length) {
                  container.innerHTML = '<div class="empty-state">No items sent to Kroger yet.</div>';
                  return;
                }

                // Group by day (newest first)
                const groups = {};
                for (const item of items) {
                  const date = item.sent_at_utc ? item.sent_at_utc.split('T')[0] : 'unknown';
                  if (!groups[date]) groups[date] = [];
                  groups[date].push(item);
                }

                // Sort days newest first
                const sortedDays = Object.keys(groups).sort((a, b) => b.localeCompare(a));

                const frag = document.createDocumentFragment();
                for (const day of sortedDays) {
                  const dayItems = groups[day].sort((a, b) => {
                    const ta = a.sent_at_utc || '';
                    const tb = b.sent_at_utc || '';
                    return tb.localeCompare(ta);
                  });

                  const dayEl = document.createElement('div');
                  dayEl.className = 'day-group';

                  const header = document.createElement('div');
                  header.className = 'day-header';
                  header.innerHTML = `<span>${escapeHtml(day)}</span><span class="count">${dayItems.length} item${dayItems.length === 1 ? '' : 's'}</span>`;
                  dayEl.appendChild(header);

                  const list = document.createElement('div');
                  list.className = 'history-list';

                  for (const item of dayItems) {
                    const card = document.createElement('div');
                    card.className = 'history-card';
                    const time = item.sent_at_utc ? new Date(item.sent_at_utc).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: true }) : '';
                    card.innerHTML = `
                      <div class="history-meta">
                        <div><strong>UPC:</strong> <span class="history-upc">${escapeHtml(item.upc || '')}</span></div>
                        <div class="history-source">${escapeHtml(item.source || '')}</div>
                        <div class="history-time">${escapeHtml(time)} · Batch: ${escapeHtml(item.batch_id || '')}</div>
                      </div>
                      <div class="history-qty">Qty ${item.quantity || 0}</div>
                    `;
                    list.appendChild(card);
                  }

                  dayEl.appendChild(list);
                  frag.appendChild(dayEl);
                }

                container.innerHTML = '';
                container.appendChild(frag);
              }

              function escapeHtml(str) {
                if (typeof str !== 'string') return '';
                return str.replace(/[&<>"']/g, m => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;','\'':'&#39;'}[m]));
              }
            })();
          </script>
        </body>
        </html>
        """;
    }
}
