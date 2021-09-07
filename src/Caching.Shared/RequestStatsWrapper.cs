using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace Caching.Shared
{
    public record struct WrappedActionResult<TResult>(TResult? Result, Exception? Exception, TimeSpan Elapsed)
    {
        public bool HasResult => Result is not null;
    }

    public class RequestStatsWrapper
    {
        public static async Task<WrappedActionResult<TResult?>> WrapAsync<TResult>(Func<Task<TResult>> action)
        {
            Exception? actionException = null;
            TResult? result = default;
            var watch = new Stopwatch();
            watch.Start();
            
            try
            {
                result = await action();
            }
            catch (Exception ex)
            {
                actionException = ex;
            };

            watch.Stop();

            return new WrappedActionResult<TResult?>(result, actionException, watch.Elapsed);
        }
    }
}
