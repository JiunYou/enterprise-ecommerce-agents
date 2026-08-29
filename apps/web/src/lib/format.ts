/**
 * 安全地格式化金額與貨幣顯示
 */
export function formatPrice(price: number, currency: string): string {
  if (typeof price !== "number" || isNaN(price)) {
    return "0.00";
  }

  const safeCurrency = (currency || "USD").trim().toUpperCase();

  try {
    return new Intl.NumberFormat("zh-TW", {
      style: "currency",
      currency: safeCurrency,
    }).format(price);
  } catch {
    return `${safeCurrency} ${price.toFixed(2)}`;
  }
}
