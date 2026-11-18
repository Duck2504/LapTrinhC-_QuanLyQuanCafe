namespace QuanLyCafe.Desktop.Models;

public enum PaymentMethod { Cash = 1, Momo = 2, VNPay = 3, Card = 4 }
public enum MovementType  { Receipt = 1, Issue = 2, Waste = 3, Sale = 4 }
public enum OrderType     { DineIn = 1, TakeAway = 2 }
public enum OrderStatus   { Open = 0, Paid = 1, Cancelled = 2 }
public enum TableStatus   { Empty = 0, Serving = 1 }
public enum OrderDetailStatus { Pending = 0, Preparing = 1, Done = 2, Delivered = 3 }