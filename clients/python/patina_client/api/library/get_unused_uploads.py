from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.unused_upload_list import UnusedUploadList
from ...types import UNSET, Unset
from typing import cast



def _get_kwargs(
    *,
    held: list[str] | Unset = UNSET,

) -> dict[str, Any]:
    

    

    params: dict[str, Any] = {}

    json_held: list[str] | Unset = UNSET
    if not isinstance(held, Unset):
        json_held = held


    params["held"] = json_held


    params = {k: v for k, v in params.items() if v is not UNSET and v is not None}


    _kwargs: dict[str, Any] = {
        "method": "get",
        "url": "/api/v1/library/uploads/unused",
        "params": params,
    }


    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | UnusedUploadList | None:
    if response.status_code == 200:
        response_200 = UnusedUploadList.from_dict(response.json())



        return response_200

    if response.status_code == 400:
        response_400 = ProblemDetails.from_dict(response.json())



        return response_400

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


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | UnusedUploadList]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    *,
    client: AuthenticatedClient | Client,
    held: list[str] | Unset = UNSET,

) -> Response[ProblemDetails | UnusedUploadList]:
    """ The files sent from another computer that no file of the library, no open document, no queued or
    running bake or build and nothing in held names any more, with the room they take. held is what the
    client holds unsaved, such as an open dialog's paths. One sent in the last hour counts as used.
    Changes nothing.

    Args:
        held (list[str] | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | UnusedUploadList]
     """


    kwargs = _get_kwargs(
        held=held,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    *,
    client: AuthenticatedClient | Client,
    held: list[str] | Unset = UNSET,

) -> ProblemDetails | UnusedUploadList | None:
    """ The files sent from another computer that no file of the library, no open document, no queued or
    running bake or build and nothing in held names any more, with the room they take. held is what the
    client holds unsaved, such as an open dialog's paths. One sent in the last hour counts as used.
    Changes nothing.

    Args:
        held (list[str] | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | UnusedUploadList
     """


    return sync_detailed(
        client=client,
held=held,

    ).parsed

async def asyncio_detailed(
    *,
    client: AuthenticatedClient | Client,
    held: list[str] | Unset = UNSET,

) -> Response[ProblemDetails | UnusedUploadList]:
    """ The files sent from another computer that no file of the library, no open document, no queued or
    running bake or build and nothing in held names any more, with the room they take. held is what the
    client holds unsaved, such as an open dialog's paths. One sent in the last hour counts as used.
    Changes nothing.

    Args:
        held (list[str] | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | UnusedUploadList]
     """


    kwargs = _get_kwargs(
        held=held,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    *,
    client: AuthenticatedClient | Client,
    held: list[str] | Unset = UNSET,

) -> ProblemDetails | UnusedUploadList | None:
    """ The files sent from another computer that no file of the library, no open document, no queued or
    running bake or build and nothing in held names any more, with the room they take. held is what the
    client holds unsaved, such as an open dialog's paths. One sent in the last hour counts as used.
    Changes nothing.

    Args:
        held (list[str] | Unset):

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | UnusedUploadList
     """


    return (await asyncio_detailed(
        client=client,
held=held,

    )).parsed
