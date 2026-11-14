namespace QuanLyCafe.Domain.Ordering;

public enum TableStatus { Empty = 0, Serving = 1 }

public enum OrderType { DineIn = 1, Takeaway = 2 }

public enum OrderStatus { Open = 0, Paid = 1, Cancelled = 2 }

public enum OrderDetailStatus { Pending = 0, Preparing = 1, Done = 2, Delivered = 3 }
