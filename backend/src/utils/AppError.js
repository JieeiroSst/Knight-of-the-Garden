// Loi "co the du doan" (sai mat khau/thieu du lieu/khong tim thay...) - PHAN BIET voi loi lap
// trinh/loi ha tang that su (bug, mat ket noi DB) qua co "isOperational". errorHandler.js dung
// co nay de quyet dinh: tra THANG message cho client (an toan, da biet truoc noi dung) hay che
// di sau thong bao chung "Loi may chu" (tranh lo chi tiet noi bo khi loi ngoai du kien).
class AppError extends Error {
  constructor(statusCode, message, details) {
    super(message);
    this.statusCode = statusCode;
    this.isOperational = true;
    this.details = details;
  }
}

module.exports = { AppError };
