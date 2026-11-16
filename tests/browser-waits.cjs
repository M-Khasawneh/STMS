// Wait for observable completion, never a guessed animation/network delay.
async function settled(page) {
  // Allow queued ResizeObserver/media-query handlers to commit their next layout frame.
  await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
  await page.waitForFunction(() => document.fonts.status === 'loaded' &&
    document.getAnimations().every(a => a.effect?.getTiming().iterations === Infinity || a.playState !== 'running'));
}
async function notificationCount(page, count) {
  await page.waitForFunction(value => {
    const badge = document.querySelector('.notification-count');
    return badge && (value === 0 ? badge.hidden : !badge.hidden && badge.textContent.trim() === String(value));
  }, count);
}
async function notificationItems(page, count) {
  await page.waitForFunction(value => document.querySelectorAll('.notification-menu-list > li').length === value, count);
}
module.exports = { settled, notificationCount, notificationItems };
