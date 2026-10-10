<script lang="ts">
	// Диалог подтверждения удаления.
	import BaseDialog from './BaseDialog.svelte';

	let { message, confirmLabel = 'Удалить', onconfirm, onclose }: {
		message: string;
		confirmLabel?: string;
		onconfirm: () => void;
		onclose: () => void;
	} = $props();
</script>

<BaseDialog open={true} title="Подтверждение удаления" closeOnOverlay onclose={onclose}>
	<div class="flex flex-col gap-5">
		<p class="text-sm text-slate-700 dark:text-slate-200">{message}</p>

		<div class="flex justify-end gap-2">
			<button type="button" class="btn" onclick={onclose}>Отмена</button>
			<button type="button" class="btn btn-danger" onclick={onconfirm}>
				<i class="fa-solid fa-trash" aria-hidden="true"></i>{confirmLabel}
			</button>
		</div>
	</div>
</BaseDialog>
