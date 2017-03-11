using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocketHttpListener.Net;

namespace Microsoft.AspNetCore.Server.SocketHttpListener
{
	public class SocketHttpListenerServer : IServer
	{
		private readonly HttpListener _listener;
		private readonly ILogger _logger;
		private readonly Action<object> _processRequest;
		private IHttpApplication<object> _application;

		public IFeatureCollection Features { get; } = new FeatureCollection();

		public SocketHttpListenerServer(
			IOptions<SocketHttpListenerOptions> options,
			ILoggerFactory loggerFactory)
		{
			_processRequest = ProcessRequestAsync;
			_logger = loggerFactory.CreateLogger(typeof(SocketHttpListenerServer).FullName);
			_listener = new HttpListener(
				new SocketHttpListenerLogger(loggerFactory),
				options.Value.Certificate)
			{
				OnContext = OnContext
			};

			Features.Set<IServerAddressesFeature>(new ServerAddressesFeature());
		}

		public void Start<TContext>(IHttpApplication<TContext> application)
		{
			if (application == null)
				throw new ArgumentNullException(nameof(application));

			_application = new ApplicationWrapper<TContext>(application);

			_listener.Prefixes.Clear();
			foreach (var address in Features.Get<IServerAddressesFeature>().Addresses)
			{
				var withPath = address.EndsWith("/") ? address : address + "/";
				_listener.Prefixes.Add(withPath);
			}
			_listener.Start();
		}

		public void Dispose()
		{
			_listener.Stop();
		}

		private void OnContext(HttpListenerContext context)
		{
			try
			{
				var ignored = Task.Factory.StartNew(_processRequest, context);
			}
			catch (Exception ex)
			{
				// Request processing failed to be queued in threadpool
				// Log the error message, release throttle and move on
				_logger.LogError(0, ex, "OnContext");
			}
		}

		private async void ProcessRequestAsync(object contextObj)
		{
			var context = (HttpListenerContext) contextObj;
			try
			{
				object appContext = null;
				var featureContext = new FeatureContext(context);
				try
				{
					appContext = _application.CreateContext(featureContext.Features);
					try
					{
						await _application.ProcessRequestAsync(appContext);
						await featureContext.OnStart();
						_application.DisposeContext(appContext, null);
					}
					finally
					{
						await featureContext.OnCompleted();
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(0, ex, "ProcessRequestAsync");
					if (featureContext.Features.Get<IHttpResponseFeature>().HasStarted)
					{
						context.Response.Abort();
					}
					else
					{
						// We haven't sent a response yet, try to send a 500 Internal Server Error
						context.Response.Headers.Clear();
						context.Response.StatusCode = 500;
						context.Response.ContentLength64 = 0;

						using (var writer = new StreamWriter(context.Response.OutputStream))
							writer.Write(ex.ToString());
					}
					_application.DisposeContext(appContext, ex);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(0, ex, "Outer ProcessRequestAsync");
				context.Response.Abort();
			}
			finally
			{
				context.Response.Close();
			}
		}

		private class ApplicationWrapper<TContext> : IHttpApplication<object>
		{
			private readonly IHttpApplication<TContext> _application;

			public ApplicationWrapper(IHttpApplication<TContext> application)
			{
				_application = application;
			}

			public object CreateContext(IFeatureCollection contextFeatures)
			{
				return _application.CreateContext(contextFeatures);
			}

			public void DisposeContext(object context, Exception exception)
			{
				_application.DisposeContext((TContext) context, exception);
			}

			public Task ProcessRequestAsync(object context)
			{
				return _application.ProcessRequestAsync((TContext) context);
			}
		}
	}
}