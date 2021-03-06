using System;
using System.Diagnostics;
using System.IO;
using json_converter;
using NetMQ;
using NetMQ.Sockets;

namespace MainApp
{
	class ServiceStarter : Process	
	{
		RequestSocket client;
		string service_addres;
		int free_sock_numb;
		public ServiceStarter() : base()
		{
			var free_sock = new ResponseSocket();
			free_sock_numb = free_sock.BindRandomPort("tcp://*");
			Console.WriteLine("free socket is {0}", free_sock_numb);
			free_sock.Close();
			service_addres = String.Format("tcp://localhost:{0}", free_sock_numb);
			this.StartInfo.FileName = "python";
			this.StartInfo.Arguments = String.Format(@" C:\programming\TopLevelProgram\cnn_service\start.py --port {0}", free_sock_numb);
			this.StartInfo.WorkingDirectory = @"C:\programming\TopLevelProgram\cnn_service\";
			this.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
			this.StartInfo.UseShellExecute = false;
			this.StartInfo.RedirectStandardOutput = true; 
			this.StartInfo.RedirectStandardError = true;
			string cnn_outputPath = @"./log/output_cnn.txt";
			using (StreamWriter sw = new StreamWriter(cnn_outputPath, false, System.Text.Encoding.Default))
			{
				sw.WriteLine(DateTime.Now);
			}
			this.OutputDataReceived += new DataReceivedEventHandler( (s, e) => {
				if (!String.IsNullOrEmpty(e.Data))
					using (StreamWriter sw = new StreamWriter(cnn_outputPath, true, System.Text.Encoding.Default))
					{
						sw.WriteLine(e.Data);
					}
			});
			string cnn_errorPath = @"./log/error_cnn.txt";
			using (StreamWriter sw = new StreamWriter(cnn_errorPath, false, System.Text.Encoding.Default))
			{
				sw.WriteLine(DateTime.Now);
			}
			this.ErrorDataReceived += new DataReceivedEventHandler((s, e) => {
				if (!String.IsNullOrEmpty(e.Data))
					using (StreamWriter sw = new StreamWriter(cnn_errorPath, true, System.Text.Encoding.Default))
					{
						sw.WriteLine(e.Data);
					}
			});


		}

		public void StartService()
		{
			this.Start();
			this.BeginOutputReadLine();
			this.BeginErrorReadLine();	

			client = new RequestSocket();
			client.Connect(service_addres);
		}
		public iResponse ProcessResponse(ServiceTask task)
		{
			var resp = new ServiceTask();
			resp.command = "processed resp";

			if(client == null)
			{
				client = new RequestSocket();
				client.Connect(service_addres);
			}
			CNNTask cnn_task = new CNNTask();
			cnn_task.image = Capture.getImage();
			string cnn_task_str = json_converter.JsonConverter.serialaze(cnn_task);
			string response_cnn_message;
			if (client.TrySendFrame(System.TimeSpan.FromSeconds(5), cnn_task_str) && 
				client.TryReceiveFrameString(System.TimeSpan.FromSeconds(5), out response_cnn_message))
			{
				var response_cnn_obj = json_converter.JsonConverter.deserialaze(response_cnn_message);
				if(Object.ReferenceEquals(response_cnn_obj.GetType(), typeof(CNNAnswer)))
				{
					CNNAnswer cnn_rec_answ = response_cnn_obj as CNNAnswer;
					System.Console.WriteLine("service resp.res: {0}", cnn_rec_answ.res);
					return cnn_rec_answ;
				}
				else
				{
					System.Console.WriteLine("server illegal answer");
					resp.command = "service illegal answer";                                      
				}
			}
			else
			{
				client = null;
				System.Console.WriteLine("service is down");
				resp.command = "service is down";
			}
			return resp;
		}
		public void KillService()
		{
			var kill_command = new ServiceTask();
			kill_command.command = "kill";
			
			string kill_message = json_converter.JsonConverter.serialaze(kill_command);
			bool service_up = client?.TrySendFrame(System.TimeSpan.FromSeconds(2), kill_message) ?? false;
			string response_cnn_message = "";
			if(service_up && 
				client.TryReceiveFrameString(System.TimeSpan.FromSeconds(5), out response_cnn_message))
			{
				Console.WriteLine("kill command was sent to cnn service");

			}
			else
			{
				Console.WriteLine("cnn service hard kill");
				this.Kill();
			}
		}
	}
}
