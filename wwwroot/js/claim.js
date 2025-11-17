// Auto-calc total payment on the Submit claim form
document.addEventListener('DOMContentLoaded', function () {
    var hours = document.querySelector('[name="HoursWorked"]');
    var rate = document.querySelector('[name="HourlyRate"]');
    var total = document.querySelector('#TotalPaymentDisplay');
    var totalInput = document.querySelector('[name="TotalPayment"]');

    function calc() {
        var h = parseFloat(hours?.value || 0);
        var r = parseFloat(rate?.value || 0);
        var t = 0;
        if (!isNaN(h) && !isNaN(r)) {
            t = Math.round((h * r + Number.EPSILON) * 100) / 100;
        }
        if (total) total.textContent = t.toFixed(2);
        if (totalInput) totalInput.value = t;
    }

    if (hours) hours.addEventListener('input', calc);
    if (rate) rate.addEventListener('input', calc);
    calc();

    // File validation helper
    var fileInput = document.querySelector('[name="supportingDocument"]');
    if (fileInput) {
        fileInput.addEventListener('change', function (e) {
            var file = e.target.files[0];
            if (!file) return;
            var allowed = ['application/pdf', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'];
            if (file.size > 5 * 1024 * 1024) {
                alert('File too large (max 5 MB).');
                fileInput.value = '';
            } else if (!allowed.includes(file.type)) {
                alert('Unsupported file type. Allowed: PDF, DOC, DOCX.');
                fileInput.value = '';
            }
        });
    }
});