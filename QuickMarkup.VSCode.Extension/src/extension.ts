import * as vscode from 'vscode';

export function activate(context: vscode.ExtensionContext) {
	console.log('QuickMarkup extension activated');

	// Just make diagnostic ranges painfully obvious for the POC.
	const diagnosticDecoration =
		vscode.window.createTextEditorDecorationType({
			backgroundColor: 'rgba(255, 0, 0, 0.25)',
			border: '1px solid red',
		});

	context.subscriptions.push(diagnosticDecoration);

	function updateEditor(editor: vscode.TextEditor | undefined) {
		if (!editor || editor.document.languageId !== 'csharp') {
			return;
		}

		const diagnostics =
			vscode.languages.getDiagnostics(editor.document.uri);

		console.log(
			`Diagnostics for ${editor.document.uri.fsPath}:`,
			diagnostics
		);

		const ranges =
			diagnostics
			// .filter(diagnostic => diagnostic.severity == vscode.DiagnosticSeverity.Information)
			.map(diagnostic => diagnostic.range);

		editor.setDecorations(diagnosticDecoration, ranges);
	}

	// Diagnostics can change asynchronously after Roslyn/the generator runs.
	context.subscriptions.push(
		vscode.languages.onDidChangeDiagnostics(event => {
			for (const uri of event.uris) {
				const editor = vscode.window.visibleTextEditors.find(
					editor => editor.document.uri.toString() === uri.toString()
				);

				if (editor) {
					updateEditor(editor);
				}
			}
		})
	);

	// Also handle an editor that was already open when the extension activates.
	context.subscriptions.push(
		vscode.window.onDidChangeActiveTextEditor(updateEditor)
	);

	updateEditor(vscode.window.activeTextEditor);

	// Keep your existing command if you want it.
	const disposable = vscode.commands.registerCommand(
		'quickmarkup.helloWorld',
		() => {
			vscode.window.showInformationMessage(
				'Hello World from quickmarkup!'
			);
		}
	);

	context.subscriptions.push(disposable);
}

export function deactivate() {}