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
	const serverDir = path.join(
		context.extensionUri.fsPath,
		'..',
		'QuickMarkup.LanguageServer'
	);

	const debugPort = process.env.QMUI_LSP_DEBUG;
	const serverOptions: ServerOptions = debugPort
		? () => {
				const socket = net.createConnection({ port: Number(debugPort) });
				return new Promise<StreamInfo>((resolve) => {
					socket.on('connect', () => resolve({ reader: socket, writer: socket }));
				});
		  }
		: {
				command: 'dotnet',
				args: ['run', '--project', serverDir],
		  };

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
