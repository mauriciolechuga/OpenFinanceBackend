using WebAPI.OpenFinance.Data;
using WebAPI.OpenFinance.Helpers;
using WebAPI.OpenFinance.Models;

namespace WebAPI.OpenFinance.Routes
{
    public static class ClientRoute
    {
        public static void ClientRoutes(this WebApplication app)
        {
            var route = app.MapGroup("clients");

            // GET /clients/{clientID}/PortfolioTotalAmount
            // Returns the per-product totals, the overall total, the clientID, and a UTC timestamp.
            route.MapGet("/{clientID}/PortfolioTotalAmount", async (OpenFinanceContext context, int clientID) =>
            {
                if (!await ClientHelper.CheckClientExists(context, clientID))
                {
                    return Results.BadRequest("Client not found");
                }

                if (!await ClientHelper.CheckClientConnections(context, clientID))
                {
                    return Results.BadRequest("Client has no connections");
                }

                var productTotals = new List<object>();
                decimal totalAmount = 0;

                // Cash is summed directly from cash_info; it is part of the overall total
                // but is not currently itemized in the per-product breakdown.
                var cashTotal = await ClientHelper.GetClientCashTotalAmount(context, clientID);
                totalAmount += cashTotal;

                var stockTotal = await ClientHelper.GetClienStockTotalAmount(context, clientID);
                totalAmount += stockTotal;
                productTotals.Add(new { product = "Stock", total = stockTotal });

                var mutualFundTotal = await ClientHelper.GetClientMutualFundTotalAmount(context, clientID);
                totalAmount += mutualFundTotal;
                productTotals.Add(new { product = "Mutual Fund", total = mutualFundTotal });

                var response = new
                {
                    clientID = clientID,
                    totalAmount = totalAmount,
                    productTotals = productTotals,
                    // Always serialize timestamps in UTC, on both backend and frontend.
                    timestamp = DateTime.UtcNow
                };

                return Results.Ok(response);
            });

            // GET /clients/{clientID}/AssetsSummary
            // For each product: total value and its percentage of the portfolio.
            // Returns the product count, overall total, per-product details, clientID, and a UTC timestamp.
            route.MapGet("/{clientID}/AssetsSummary", async (OpenFinanceContext context, int clientID) =>
            {
                if (!await ClientHelper.CheckClientExists(context, clientID))
                {
                    return Results.BadRequest("Client not found");
                }

                if (!await ClientHelper.CheckClientConnections(context, clientID))
                {
                    return Results.BadRequest("Client has no connections");
                }

                var productDetail = new List<ProductDetails>();

                var stockTotal = await ClientHelper.GetClienStockTotalAmount(context, clientID);
                productDetail.Add(new ProductDetails { ProductName = "Stock", ProdTotal = stockTotal });

                var mutualFundTotal = await ClientHelper.GetClientMutualFundTotalAmount(context, clientID);
                productDetail.Add(new ProductDetails { ProductName = "Mutual Fund", ProdTotal = mutualFundTotal });

                // Fills in PortfolioPercentage on each entry.
                ClientHelper.CalculatePercentageForEachProduct(productDetail);

                int numProducts = ClientHelper.GetNumProducts(productDetail);
                decimal totalAmount = ClientHelper.CalculateTotalAmount(productDetail);

                if (numProducts == 0)
                {
                    return Results.BadRequest("Client has no products");
                }

                var response = new
                {
                    clientID = clientID,
                    numProducts = numProducts,
                    totalAmount = totalAmount,
                    productDetails = productDetail,
                    timestamp = DateTime.UtcNow
                };

                return Results.Ok(response);
            });
        }
    }
}
