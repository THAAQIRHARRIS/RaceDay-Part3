// Register page: only show the organiser code when "Organiser" is selected.
(function () {
  var radios = document.querySelectorAll('input[name="Role"]');
  var box = document.getElementById('organiser-code-box');
  if (!radios.length || !box) return;

  function sync() {
    var selected = document.querySelector('input[name="Role"]:checked');
    box.style.display = selected && selected.value === 'Organiser' ? 'block' : 'none';
  }
  radios.forEach(function (r) { r.addEventListener('change', sync); });
  sync();
})();
