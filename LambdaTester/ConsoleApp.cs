using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using hotslambda;
using Microsoft.Extensions.DependencyInjection;
using static LambdaTester.Program;

namespace LambdaTester
{
    class ConsoleApp
    {
        public static async Task Run()
        {
            var apiSettings = ServiceProviderProvider.GetService<ApiSettings>();

            var result = await Function.MainAsync(apiSettings.lambda_parser_endpoint_url,
                    apiSettings.lambda_parser_endpoint_access, apiSettings.lambda_parser_endpoint_secret, apiSettings.fingerprint);
        }
    }
}
