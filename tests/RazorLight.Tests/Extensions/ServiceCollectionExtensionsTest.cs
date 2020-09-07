﻿#if !(NETCOREAPP2_0)
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
#endif
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using RazorLight.Extensions;
using System;
using RazorLight.Compilation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Reflection;
using System.Threading.Tasks;
using System.Dynamic;
using System.IO;
using RazorLight.Razor;

namespace RazorLight.Tests.Extensions
{
	public class ServiceCollectionExtensionsTest
	{
		private string rootPath = PathUtility.GetViewsPath();

		private IServiceCollection GetServices()
		{
			var services = new ServiceCollection();
			var envMock = new Mock<Microsoft.AspNetCore.Hosting.IHostingEnvironment>();
			envMock.Setup(m => m.ContentRootPath).Returns(rootPath);
			services.AddSingleton<Microsoft.AspNetCore.Hosting.IHostingEnvironment>(envMock.Object);

			return services;
		}

		[Fact]
		public void Throws_On_Null_EngineFactoryProvider()
		{
			var services = GetServices();

			Assert.Throws<ArgumentNullException>(() => { services.AddRazorLight(null); });
		}

		[Fact]
		public void Ensure_FactoryMethod_Is_Called()
		{
			var services = GetServices();
			bool called = false;

			services.AddRazorLight(() =>
			{
				called = true;
				return new RazorLightEngineBuilder().UseEmbeddedResourcesProject(typeof(Root).Assembly).Build();
			});

			var provider = services.BuildServiceProvider();
			var engine = provider.GetService<IRazorLightEngine>();

			Assert.NotNull(engine);
			Assert.IsType<RazorLightEngine>(engine);
			Assert.True(called);
		}

		public class EmbeddedEngineStartup
		{
			public void Configure(IApplicationBuilder app)
			{

			}
			public void ConfigureServices(IServiceCollection services)
			{
				var embeddedEngine = new RazorLightEngineBuilder()
					.UseEmbeddedResourcesProject(typeof(EmbeddedEngineStartup)) // exception without this (or another project type)
					.UseMemoryCachingProvider()
					.Build();

				services.AddRazorLight(() => embeddedEngine);
			}
		}

#if !(NETCOREAPP2_0)
		[Fact]
		public void Ensure_Works_With_Generic_Host()
		{
			static IHostBuilder CreateHostBuilder(string[] args)
			{
				return Host.CreateDefaultBuilder(args)
					.ConfigureWebHostDefaults(webBuilder =>
					{
						webBuilder.UseStartup<EmbeddedEngineStartup>();
					});
			}

			var hostBuilder = CreateHostBuilder(null);

			Assert.NotNull(hostBuilder);
			var host = hostBuilder.Build();
			Assert.NotNull(host);
			host.Services.GetService<IRazorLightEngine>();
		}
#endif

		[Fact]
		public void Ensure_RazorLightEngineWithFileSystemFactory_Is_Called()
		{
			var services = GetServices();
			var called = false;

			services.AddRazorLight(() =>
			{
				called = true;
				return new RazorLightEngineWithFileSystemProjectFactory().Create();
			});

			var provider = services.BuildServiceProvider();
			var engine = provider.GetService<IRazorLightEngine>();

			Assert.NotNull(engine);
			Assert.IsType<RazorLightEngine>(engine);
			Assert.True(called);
		}

		[Fact]
		public void Ensure_DI_Extension_Can_Inject()
		{
			var services = GetServices();			
			bool newRazorLightEngineCalled = false;
			var root = "C:";

			services.AddRazorLight()
				.UseMemoryCachingProvider()
				.UseFileSystemProject(root)
				.UseNetFrameworkLegacyFix();
			


			services.RemoveAll<IMetadataReferenceManager>();
			services.AddSingleton<IMetadataReferenceManager>(new TestMetadataReferenceManager(() =>
			{
				
			}));

			services.RemoveAll<IRazorLightEngine>();
			services.AddSingleton<IRazorLightEngine>(new TestRazorLightEngine(() =>
			{
				newRazorLightEngineCalled = true;
			}));

			var provider = services.BuildServiceProvider();
			var directoryFormatter = provider.GetService<IAssemblyDirectoryFormatter>();
			Assert.IsType<LegacyFixAssemblyDirectoryFormatter>(directoryFormatter);

			var project = provider.GetService<RazorLightProject>();
			Assert.IsType<FileSystemRazorProject>(project);
			var fileSystemProject = project as FileSystemRazorProject;
			Assert.Equal(fileSystemProject.Root, root);

			var engine = provider.GetService<IRazorLightEngine>();
			Assert.NotNull(engine);
			Assert.IsType<TestRazorLightEngine>(engine);			
			engine.CompileRenderStringAsync("","","").GetAwaiter().GetResult();
			Assert.True(newRazorLightEngineCalled); 
		
			Assert.IsType<TestMetadataReferenceManager>(provider.GetService<IMetadataReferenceManager>());				
		}

		public class TestMetadataReferenceManager : IMetadataReferenceManager
		{

			private Action _resolveAction = null;
			public TestMetadataReferenceManager(Action resolveAction)
			{
				_resolveAction = resolveAction;
			}

			public HashSet<MetadataReference> AdditionalMetadataReferences {
				get 
				{
					return new HashSet<MetadataReference>();
				} 
			}

			public IReadOnlyList<MetadataReference> Resolve(Assembly assembly)
			{
				_resolveAction();
				return new List<MetadataReference>();
			}
		}

		public class TestRazorLightEngine : IRazorLightEngine
		{

			private Action _compileAction = null;
			public TestRazorLightEngine(Action compileAction)
			{
				_compileAction = compileAction;
			}

			public RazorLightOptions Options => new RazorLightOptions();

			public IEngineHandler Handler => throw new NotImplementedException();

			public Task<string> CompileRenderAsync<T>(string key, T model, ExpandoObject viewBag = null)
			{
				throw new NotImplementedException();
			}

			public Task<string> CompileRenderAsync(string key, object model, Type modelType, ExpandoObject viewBag = null)
			{
				throw new NotImplementedException();
			}

			public Task<string> CompileRenderStringAsync<T>(string key, string content, T model, ExpandoObject viewBag = null)
			{
				_compileAction();
				var result = nameof(TestRazorLightEngine);
				return Task.FromResult(result);
			}

			public Task<ITemplatePage> CompileTemplateAsync(string key)
			{
				throw new NotImplementedException();
			}

			public Task<string> RenderTemplateAsync(ITemplatePage templatePage, object model, Type modelType, ExpandoObject viewBag = null)
			{
				throw new NotImplementedException();
			}

			public Task<string> RenderTemplateAsync<T>(ITemplatePage templatePage, T model, ExpandoObject viewBag = null)
			{
				throw new NotImplementedException();
			}

			public Task RenderTemplateAsync(ITemplatePage templatePage, object model, Type modelType, TextWriter textWriter, ExpandoObject viewBag = null)
			{
				throw new NotImplementedException();
			}
		}

		[Fact]
		public void Try_Render_With_DI_Extension()
		{
			var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);		

			var services = GetServices();
			services.AddRazorLight()
				.UseMemoryCachingProvider()
				.UseFileSystemProject(Path.Combine(path, "Assets", "Files"));

			var provider = services.BuildServiceProvider();
			var engine = provider.GetService<IRazorLightEngine>();
			var result = engine.CompileRenderAsync<object>("template1.cshtml", null).GetAwaiter().GetResult();




		}
	}
}
