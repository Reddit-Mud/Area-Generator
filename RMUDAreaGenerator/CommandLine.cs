using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RMUD
{
	public class CommandLine
	{
		public enum Error
		{
			Success = 0,
			UnknownOption,
			NoValue,
			BadValue,
		}

		public static Error ParseCommandLine(Object CommandLineOptions)
		{
			var ArgsType = CommandLineOptions.GetType();

			String[] Arguments = Environment.GetCommandLineArgs();

			for (int i = 1; i < Arguments.Length; )
			{
				String ArgName = Arguments[i].ToUpper();
				++i;

                var Property = ArgsType.GetProperty(ArgName);

                if (Property == null)
                    throw new InvalidOperationException("Unknown command line option " + ArgName);

				if (Property.PropertyType == typeof(Boolean))
					Property.SetValue(CommandLineOptions, true, null);
				else
				{
                    if (i >= Arguments.Length) throw new InvalidOperationException("No value given for command line option " + ArgName);
					
						Property.SetValue(CommandLineOptions, System.Convert.ChangeType(Arguments[i], Property.PropertyType), null);
					
					++i;
				}
			}

			return Error.Success;
		}
	}
}
