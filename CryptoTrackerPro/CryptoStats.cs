using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoTrackerPro
{
    public static class CryptoStats
    {
        public static double CalculateAverage(IEnumerable<double> prices)
            => prices.Any() ? prices.Average() : 0;

        public static double CalculateVolatility(IEnumerable<double> prices)
        {
            if (prices.Count() < 2) return 0;
            double avg = prices.Average();
            double sum = prices.Sum(d => Math.Pow(d - avg, 2));
            return Math.Sqrt(sum / (prices.Count() - 1));
        }

        public static double CalculateRSI(IEnumerable<double> prices)
        {
            var priceList = prices.ToList();
            if (priceList.Count < 15) return 50; 

            double gains = 0;
            double losses = 0;

            for (int i = 1; i < priceList.Count; i++)
            {
                double diff = priceList[i] - priceList[i - 1];
                if (diff >= 0) gains += diff;
                else losses -= diff;
            }

            if (losses == 0) return 100;
            double rs = gains / losses;
            return 100 - (100 / (1 + rs));
        }
    }
}
