from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.history_jump import HistoryJump
from ...models.problem_details import ProblemDetails
from ...models.stack_edit_result import StackEditResult
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: HistoryJump,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/stacks/{id}/history".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> ProblemDetails | StackEditResult | None:
    if response.status_code == 200:
        response_200 = StackEditResult.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx.Response) -> Response[ProblemDetails | StackEditResult]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: HistoryJump,

) -> Response[ProblemDetails | StackEditResult]:
    """ Undoes or redoes until a step is the newest that stands, as one event.

    Args:
        id (str):
        body (HistoryJump): Where to take an undo history: undoing or redoing until a step is the
            newest that stands.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackEditResult]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: HistoryJump,

) -> ProblemDetails | StackEditResult | None:
    """ Undoes or redoes until a step is the newest that stands, as one event.

    Args:
        id (str):
        body (HistoryJump): Where to take an undo history: undoing or redoing until a step is the
            newest that stands.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackEditResult
     """


    return sync_detailed(
        id=id,
client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: HistoryJump,

) -> Response[ProblemDetails | StackEditResult]:
    """ Undoes or redoes until a step is the newest that stands, as one event.

    Args:
        id (str):
        body (HistoryJump): Where to take an undo history: undoing or redoing until a step is the
            newest that stands.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackEditResult]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: HistoryJump,

) -> ProblemDetails | StackEditResult | None:
    """ Undoes or redoes until a step is the newest that stands, as one event.

    Args:
        id (str):
        body (HistoryJump): Where to take an undo history: undoing or redoing until a step is the
            newest that stands.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackEditResult
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
