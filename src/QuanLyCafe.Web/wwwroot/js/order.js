// Trang gọi món tại bàn: nút +/−, tính tổng tạm, lọc món không dấu. Không có JS form vẫn gửi được.
(() => {
  const form = document.getElementById('order-form');
  if (!form) return;
  const lines = [...form.querySelectorAll('.line')];
  const vnd = new Intl.NumberFormat('vi-VN');
  const plain = s => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();

  const qtyOf = line => line.querySelector('input[type=number]');
  const update = () => {
    let count = 0, total = 0;
    for (const line of lines) {
      const q = Math.max(0, Math.min(50, parseInt(qtyOf(line).value, 10) || 0));
      line.classList.toggle('picked', q > 0);
      count += q;
      total += q * parseFloat(line.dataset.price);
    }
    document.getElementById('cart-count').textContent = count;
    document.getElementById('cart-total').textContent = vnd.format(total) + ' ₫';
    form.querySelector('.cart-bar').classList.toggle('show', count > 0);
  };

  form.addEventListener('click', e => {
    const btn = e.target.closest('.step');
    if (!btn) return;
    const input = qtyOf(btn.closest('.line'));
    input.value = Math.max(0, Math.min(50, (parseInt(input.value, 10) || 0) + Number(btn.dataset.step)));
    update();
  });
  form.addEventListener('input', update);

  // Chặn bấm "Gửi order" hai lần liên tiếp.
  form.addEventListener('submit', () => form.querySelector('button[type=submit]').disabled = true);

  const filter = document.getElementById('filter');
  filter?.addEventListener('input', () => {
    const q = plain(filter.value.trim());
    for (const line of lines) line.hidden = q !== '' && !plain(line.dataset.name).includes(q);
    for (const group of form.querySelectorAll('.menu-group'))
      group.hidden = ![...group.querySelectorAll('.line')].some(l => !l.hidden);
  });

  update();
})();
