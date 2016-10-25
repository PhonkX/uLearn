﻿$(document).ready(function () {
	var $exerciseScoreForm = $('.exercise__score-form');
	var $exerciseSimpleScoreForm = $('.exercise__simple-score-form');
	var $scoreBlock = $('.exercise__score');
	var $otherScoreLink = $scoreBlock.find('.exercise__other-score-link');
	// TODO: multiple $otherScoreInput on page with simple score forms
	var $otherScoreInput = $scoreBlock.find('[name=exerciseScore]');

	var sendSimpleScore = function ($scoreForm, ignoreNewestSubmission) {
		ignoreNewestSubmission = ignoreNewestSubmission || false;
		var $status = $scoreForm.find('.status');
		$status.removeClass('success').removeClass('error').text('Сохраняем..').addClass('waiting');

		var postData = $scoreForm.serialize();
		if (ignoreNewestSubmission)
			postData += "&ignoreNewestSubmission=true";

		$.ajax({
			type: 'post',
			url: $scoreForm.attr('action'),
			data: postData
		}).success(function (data) {
			if (data.status && data.status !== 'ok') {
				$status.addClass('error');
				var error = '';
				if (data.error === 'has_newest_submission') {
					error = 'Пользователь успел отправить ещё одно решение по этой задаче {NEW_SUBMISSION_DATE}. Поставить баллы ' +
						'<a href="#" data-submission-id="{SUBMISSION}" class="simple-score-link internal-page-link">старому решению</a>, ' +
						'<a href="#" data-submission-id="{NEW_SUBMISSION}" class="simple-score-link internal-page-link">новому</a> ' +
						'или <a href="#" class="cancel-link internal-page-link">отменить</a>?';
					error = error.replace('{SUBMISSION}', $scoreForm.find('[name=submissionId]').val())
						.replace('{NEW_SUBMISSION}', data.submissionId)
						.replace('{NEW_SUBMISSION_DATE}', data.submissionDate);
				} else if (data.error === 'has_greatest_score') {
					error = 'Пользователь имеет за код-ревью по этой задаче больше баллов: {SCORE}. Новыми баллами вы не понизите его суммарную оценку. Если вы хотите исправить ошибочную оценку, <a href="{URL}" target="_blank">пройдите по ссылке</a>.';
					error = error.replace('{SCORE}', data.score).replace('{URL}', data.checkedQueueUrl);
				}
				$status.html(error);
			} else {
				$status.addClass('success').text('Сохранено: ' + data.score);
			}
		}).always(function() {
			$status.removeClass('waiting');
		});
	}

	$scoreBlock.on('click', '.simple-score-link', function(e) {
		e.preventDefault();
		var $self = $(this);
		var submissionId = $self.data('submissionId');
		var $form = $self.closest('.exercise__simple-score-form');
		$form.find('[name=submissionId]').val(submissionId);
		sendSimpleScore($form, true);
	});

	$scoreBlock.on('click', '.cancel-link', function (e) {
		e.preventDefault();
		$(this).closest('.status').text('');
	});

	$scoreBlock.on('click', '.exercise__other-score-link', function (e) {
		e.preventDefault();
		$scoreBlock.find('.btn-group .btn').removeClass('active');
		$otherScoreInput.show();
		$otherScoreInput.focus();
		$otherScoreLink.addClass('active');
	});

	$scoreBlock.find('.btn-group').on('click', '.btn', function () {
		var $self = $(this);
		var wasActive = $self.hasClass('active');
		$scoreBlock.find('.btn-group .btn').removeClass('active');
		$self.toggleClass('active', !wasActive);

		$otherScoreInput.hide();
		$otherScoreLink.removeClass('active');
		$otherScoreInput.val(wasActive ? "" : $self.data('value'));

		if ($exerciseSimpleScoreForm.length)
			sendSimpleScore($self.closest('.exercise__simple-score-form'));
	});

	$exerciseScoreForm.find('input[type=submit]').click(function() {
		if ($otherScoreInput.is(':invalid')) {
			$otherScoreInput.show();
			$otherScoreLink.addClass('active');
		}
	});

	$('.exercise__add-review').each(function () {
		var $topReviewComments = $('.exercise__top-review-comments');
		if ($topReviewComments.find('.comment').length == 0) {
			$(this).addClass('without-comments');
			return;
		}
		var $topComments = $topReviewComments.clone(true).removeClass('hidden');
		$('.exercise__add-review__top-comments').append($topComments);
	});

	$('.exercise__top-review-comments .comment a').click(function(e) {
		e.preventDefault();
		$('.exercise__add-review__comment')
			.val($(this).data('value'))
			.trigger('input');
	});
	
	$('.user-submission__info').bind('move', function (e) {
		var $self = $(this);
		var left = parseInt($self.css('left'));
		$self.css({ left: left + e.deltaX });
	}).bind('moveend', function(e) {
		var $self = $(this);
		if (2 * e.distX < $self.width())
			$self.animate({ left: 15 });
		else
			$self.animate({ left: $(window).width() + 15 });
	});
});