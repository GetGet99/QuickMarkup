import * as fs from 'fs';
import * as net from 'net';
import * as path from 'path';
import * as vscode from 'vscode';
import {
	LanguageClient,
	LanguageClientOptions,
	ServerOptions,
	StreamInfo,
} from 'vscode-languageclient/node';

let client: LanguageClient | undefined;

export function activate(context: vscode.ExtensionContext) {
	const logChannel = vscode.window.createOutputChannel('QuickMarkup Extension', { log: true });
	logChannel.appendLine('Extension started');
	const bundledServerDir = path.join(context.extensionUri.fsPath, 'server');
	const bundledServerDll = path.join(bundledServerDir, 'QuickMarkup.LanguageServer.dll');

	const debugPort = process.env.QMUI_LSP_DEBUG;
	logChannel.appendLine(`debugPort = ${debugPort}`);
	const serverOptions: ServerOptions = debugPort
		? () => connectWithRetry(Number(debugPort))
		: fs.existsSync(bundledServerDll)
			? {
				command: 'dotnet',
				args: [bundledServerDll],
			}
			: {
				command: 'dotnet',
				args: [
					'run', '--project',
					path.join(context.extensionUri.fsPath, '..', 'QuickMarkup.LanguageServer'),
				],
			};

	logChannel.appendLine(`serverOptions = ${JSON.stringify(serverOptions)}`);
	const clientOptions: LanguageClientOptions = {
		documentSelector: [{ language: 'quickmarkup' }],
		initializationOptions: {
			workspaceRoot: vscode.workspace.workspaceFolders?.[0]?.uri?.fsPath,
		},
	};

	client = new LanguageClient(
		'quickmarkup-lsp',
		'QuickMarkup Language Server',
		serverOptions,
		clientOptions
	);

	client.start();
}

export function deactivate(): Thenable<void> | undefined {
	return client?.stop();
}

function connectWithRetry(port: number): Promise<StreamInfo> {
	return new Promise((resolve, reject) => {
		const attempt = () => {
			const socket = net.createConnection({ port });

			socket.once('connect', () => {
				console.log(`Connected to LSP on port ${port}`);
				resolve({ reader: socket, writer: socket });
			});

			socket.once('error', () => {
				console.log('LSP not ready yet, retrying...');
				setTimeout(attempt, 1000);
			});
		};

		attempt();
	});
}