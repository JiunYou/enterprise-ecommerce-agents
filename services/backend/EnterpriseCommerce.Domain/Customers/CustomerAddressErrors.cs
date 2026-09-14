using EnterpriseCommerce.Domain.Primitives;

namespace EnterpriseCommerce.Domain.Customers;

public static class CustomerAddressErrors
{
    public static readonly Error NotFound = new(
        "CustomerAddress.NotFound",
        "找不到指定的收件地址。");

    public static readonly Error InvalidId = new(
        "CustomerAddress.InvalidId",
        "地址 ID 不得為空。");

    public static readonly Error InvalidCustomerId = new(
        "CustomerAddress.InvalidCustomerId",
        "顧客 ID 不得為空。");

    public static readonly Error InvalidRecipientName = new(
        "CustomerAddress.InvalidRecipientName",
        "收件人姓名不可為空且長度不可超過 100 字元。");

    public static readonly Error InvalidPhone = new(
        "CustomerAddress.InvalidPhone",
        "聯絡電話不可為空且長度不可超過 30 字元，且不可包含控制字元。");

    public static readonly Error InvalidCountryCode = new(
        "CustomerAddress.InvalidCountryCode",
        "國碼必須為 2 碼 ASCII 英文字母。");

    public static readonly Error InvalidPostalCode = new(
        "CustomerAddress.InvalidPostalCode",
        "郵遞區號不可為空且長度不可超過 20 字元。");

    public static readonly Error InvalidCity = new(
        "CustomerAddress.InvalidCity",
        "城市/縣市不可為空且長度不可超過 100 字元。");

    public static readonly Error InvalidAddressLine1 = new(
        "CustomerAddress.InvalidAddressLine1",
        "地址第一行不可為空且長度不可超過 200 字元。");

    public static readonly Error InvalidAddressLine2 = new(
        "CustomerAddress.InvalidAddressLine2",
        "地址第二行長度不可超過 200 字元。");
}
